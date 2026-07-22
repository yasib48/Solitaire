using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Solitaire.Commands;
using Solitaire.Helpers;
using Solitaire.Services;
using UniRx;
using Zenject;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Solitaire.Models
{
    public class Game : DisposableEntity
    {
        public enum Popup
        {
            None,
            Match,
            Options,
            Leaderboard
        }

        public enum State
        {
            Home,
            Dealing,
            Playing,
            Paused,
            Win
        }

        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Card.Config _cardConfig;

        [Inject]
        private readonly Config _gameConfig;

        [Inject]
        private readonly ICardSpawner _cardSpawner;

        [Inject]
        private readonly ICommandService _commandService;

        [Inject]
        private readonly DrawCardCommand.Factory _drawCardCommandFactory;

        [Inject]
        private readonly GamePopup _gamePopup;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly IHintService _hintService;

        [Inject]
        private readonly Leaderboard _leaderboard;

        [Inject]
        private readonly ILevelService _levelService;

        [Inject]
        private readonly IMagicWandVfx _magicWandVfx;

        [Inject]
        private readonly MoveCardCommand.Factory _moveCardCommandFactory;

        [Inject]
        private readonly IMovesService _movesService;

        [Inject]
        private readonly Options _options;

        [Inject]
        private readonly IPointsService _pointsService;

        [Inject]
        private readonly ITimerService _timerService;

        [Inject]
        private readonly RefillStockCommand.Factory _refillStockCommandFactory;

        public Game()
        {
            HasStarted = new BoolReactiveProperty(false);

            RestartCommand = new ReactiveCommand();
            RestartCommand.Subscribe(_ => Restart()).AddTo(this);

            NewMatchCommand = new ReactiveCommand();
            NewMatchCommand.Subscribe(_ => NewMatchAsync().Forget()).AddTo(this);

            ContinueCommand = new ReactiveCommand(HasStarted);
            ContinueCommand.Subscribe(_ => Continue()).AddTo(this);
        }

        public BoolReactiveProperty HasStarted { get; }
        public ReactiveCommand RestartCommand { get; }
        public ReactiveCommand NewMatchCommand { get; }
        public ReactiveCommand ContinueCommand { get; }
        public Subject<GameEndResult> GameEnded { get; } = new();

        public Pile PileStock { get; private set; }
        public Pile PileWaste { get; private set; }
        public IList<Pile> PileFoundations { get; private set; }
        public IList<Pile> PileTableaus { get; private set; }
        public IList<Card> Cards { get; private set; }
        public string ActiveDailyKey { get; private set; }
        public Subject<string> DailyGameWon { get; } = new();

        private int? _pendingDailySeed;
        private string _pendingDailyKey;

        public void Init(
            Pile pileStock,
            Pile pileWaste,
            IList<Pile> pileFoundations,
            IList<Pile> pileTableaus
        )
        {
            PileStock = pileStock;
            PileWaste = pileWaste;
            PileFoundations = pileFoundations;
            PileTableaus = pileTableaus;

            SpawnCards();
            LoadLeaderboard();
        }

        public void QueueDailyMatch(string dailyKey, int seed)
        {
            ActiveDailyKey = dailyKey;
            _pendingDailyKey = dailyKey;
            _pendingDailySeed = seed;
        }

        public void RefillStock()
        {
            if (PileStock.HasCards || !PileWaste.HasCards)
            {
                PlayErrorSfx();
                return;
            }

            // Refill stock pile from waste pile
            var command = _refillStockCommandFactory.Create(PileStock, PileWaste);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
        }

        // Returns true if the card was moved. Caller handles failure feedback.
        public bool MoveCard(Card card, Pile pile)
        {
            if (card == null)
                return false;

            // Try to find valid move for the card
            if (pile == null)
                pile = _hintService.FindValidMove(card);

            // Couldn't find move
            if (pile == null)
                return false;

            // Move card to pile
            var command = _moveCardCommandFactory.Create(card, card.Pile, pile);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
            return true;
        }

        public void DrawCard()
        {
            // Draw card(s) from stock
            var command = _drawCardCommandFactory.Create(PileStock, PileWaste);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
        }

        public bool CanMagicWand()
        {
            for (var t = 0; t < PileTableaus.Count; t++)
                for (var i = 0; i < PileTableaus[t].Cards.Count; i++)
                    if (!PileTableaus[t].Cards[i].IsFaceUp.Value
                        && FindMagicTarget(PileTableaus[t].Cards[i]) != null)
                        return true;

            for (var i = 0; i < PileStock.Cards.Count; i++)
                if (!PileStock.Cards[i].IsFaceUp.Value && FindMagicTarget(PileStock.Cards[i]) != null)
                    return true;

            return false;
        }

        // Front-face sorting order the revealed card(s) are pinned to from the
        // moment they pop to the front until they've fully landed back in their
        // pile, so the reveal always stays on top instead of dipping behind
        // other cards during its flight home. Set via Card.FrontOrderOverride,
        // not Order - Order keeps driving the card's normal pile stacking.
        private const int RevealFrontOrder = 100;

        // Cinematic reveal: dims the board, then for up to two hidden cards flips
        // them face-up, lifts each to the centre with a golden glow and sparkles,
        // and finally sends it home to its pile.
        public async UniTask MagicWandAsync()
        {
            var affectedPiles = new List<Pile>();

            // Prefer face-down tableau cards. Only fall back to stock when no
            // tableau card can be revealed and moved.
            var candidates = new List<Card>();
            for (var t = 0; t < PileTableaus.Count; t++)
                for (var i = PileTableaus[t].Cards.Count - 1; i >= 0; i--)
                    if (!PileTableaus[t].Cards[i].IsFaceUp.Value)
                        candidates.Add(PileTableaus[t].Cards[i]);

            if (!candidates.Any(card => FindMagicTarget(card) != null))
                for (var i = PileStock.Cards.Count - 1; i >= 0; i--)
                    if (!PileStock.Cards[i].IsFaceUp.Value)
                        candidates.Add(PileStock.Cards[i]);

            // Pick up to two cards that actually have somewhere to go. Each card
            // must land on a *different* target pile: FindMagicTarget evaluates
            // the current board, so without this two same-rank cards (e.g. both
            // red 5s onto the same black 6) would both be judged valid and end
            // up stacked on top of each other illegally.
            var plans = new List<(Card card, Pile source, Pile target)>();
            var reservedTargets = new HashSet<Pile>();
            foreach (var card in candidates)
            {
                if (plans.Count >= 2) break;
                var target = FindMagicTarget(card);
                if (target == null || reservedTargets.Contains(target)) continue;
                plans.Add((card, card.Pile, target));
                reservedTargets.Add(target);
            }

            if (plans.Count == 0)
                return;

            var animMs = (int)(_cardConfig.AnimationDuration * 1000) + 30;

            await _magicWandVfx.FadeAsync(true);
            SetCardsDimmed(true);

            var origins = new Vector3[plans.Count];
            for (var i = 0; i < plans.Count; i++)
                origins[i] = plans[i].card.Position.Value;

            // Intro: the wand icon flies to the centre, grows, then hops over
            // each card it's about to reveal, lighting it up (yellow) as it
            // lands on it, as if selecting them.
            await _magicWandVfx.WaveWandAsync(origins, i =>
            {
                if (i >= 0 && i < plans.Count)
                    plans[i].card.Highlight.Value = 1f;
            });

            // Phase 1: pop every card to the front and flip them face-up, all
            // at the same time, right where each one sits. The yellow selection
            // highlight is cleared here so the revealed card shows its real
            // colours, not tinted yellow.
            for (var i = 0; i < plans.Count; i++)
            {
                var (card, _, _) = plans[i];
                card.Highlight.Value = 0f;
                card.IsVisible.Value = true;
                card.IsInteractable.Value = false;
                card.Dim.Value = 0f;
                card.FrontOrderOverride.Value = RevealFrontOrder;
                if (!card.IsFaceUp.Value)
                    card.Flip();
            }

            await UniTask.Delay(animMs);
            for (var i = 0; i < plans.Count; i++)
                _magicWandVfx.Sparkle(origins[i]);

            // Phase 2: lift each card to its side-by-side slot in the centre
            // of the screen one after another, so they arrive in sequence
            // instead of popping up all at once.
            var glows = new Transform[plans.Count];
            for (var i = 0; i < plans.Count; i++)
            {
                var (card, _, _) = plans[i];
                var slot = _magicWandVfx.GetRevealSlot(i, plans.Count);
                card.Scale.Value = _magicWandVfx.RevealScale;
                card.Position.Value = slot;
                glows[i] = _magicWandVfx.ShowGlow(slot);

                if (i < plans.Count - 1)
                    await UniTask.Delay(_magicWandVfx.LiftStaggerMs);
            }

            await UniTask.Delay(animMs + _magicWandVfx.HoldMs);

            // Phase 3: send every card home to its pile at the same time.
            for (var i = 0; i < plans.Count; i++)
            {
                var (card, source, target) = plans[i];

                _magicWandVfx.HideGlow(glows[i]);
                card.Scale.Value = 1f;
                target.AddCard(card);

                // FrontOrderOverride is still active from phase 1, so the card
                // stays on top through this whole return flight regardless of
                // whatever pile order AddCard just gave it.
                card.IsVisible.Value = true;
                card.IsInteractable.Value = true;

                if (source != null && !affectedPiles.Contains(source))
                    affectedPiles.Add(source);
                if (!affectedPiles.Contains(target))
                    affectedPiles.Add(target);
            }

            await UniTask.Delay(animMs);
            for (var i = 0; i < plans.Count; i++)
            {
                var card = plans[i].card;
                card.FrontOrderOverride.Value = null;
                _magicWandVfx.Sparkle(card.Position.Value);
            }

            await _magicWandVfx.FadeAsync(false);
            SetCardsDimmed(false);

            // Recompute every affected pile from scratch so waterfall offsets read
            // settled positions, not cards still mid-animation.
            for (var i = 0; i < affectedPiles.Count; i++)
                affectedPiles[i].UpdatePosition(affectedPiles[i].Position);

            // The wand rearranges the board directly (it pulls out a hidden,
            // possibly buried card) without going through the command system, so
            // the existing undo stack now refers to a board that no longer
            // matches. Undoing a prior move against this changed board flips the
            // wrong card and leaves face-up/face-down cards mis-layered. Clear
            // the history so undo can't corrupt the board after a wand reveal.
            _commandService.Reset();
        }

        private Pile FindMagicTarget(Card card)
        {
            // Foundation first
            for (var f = 0; f < PileFoundations.Count; f++)
            {
                var top = PileFoundations[f].TopCard();
                if (top == null && card.Type == Card.Types.Ace)
                    return PileFoundations[f];
                if (top != null && top.Suit == card.Suit && top.Type == card.Type - 1)
                    return PileFoundations[f];
            }

            // Then tableau
            for (var t = 0; t < PileTableaus.Count; t++)
            {
                var top = PileTableaus[t].TopCard();
                if (top == null && card.Type == Card.Types.King)
                    return PileTableaus[t];
                if (top != null && top.Type == card.Type + 1 && IsOppositeColor(card, top))
                    return PileTableaus[t];
            }

            return null;
        }

        public void PlayErrorSfx()
        {
            _audioService.PlaySfx(Audio.SfxError, 0.5f);
        }

        public void DetectWinCondition()
        {
            if (Cards == null)
                return;

            // Real win only once every card is actually on a foundation. The
            // "all cards revealed" state used to auto-win here; now it just
            // lights up the Complete button (see CanAutoComplete) and the game
            // waits for the player - either they place the rest by hand, or
            // press Complete to auto-finish.
            for (var i = 0; i < Cards.Count; i++)
                if (!(Cards[i].IsInPile && Cards[i].Pile.IsFoundation))
                    return;

            WinAsync().Forget();
        }

        // True once every hidden card in the TABLEAU piles has been revealed
        // (the stock may still hold cards - those don't block it) while at least
        // one card still needs to reach a foundation. Drives the Complete button.
        public bool CanAutoComplete()
        {
            if (Cards == null || _gameState.State.Value != State.Playing)
                return false;

            // Any face-down card still sitting in a tableau blocks it.
            for (var t = 0; t < PileTableaus.Count; t++)
                for (var j = 0; j < PileTableaus[t].Cards.Count; j++)
                    if (!PileTableaus[t].Cards[j].IsFaceUp.Value)
                        return false;

            // At least one card still off the foundations means there's work left.
            for (var i = 0; i < Cards.Count; i++)
                if (!(Cards[i].IsInPile && Cards[i].Pile.IsFoundation))
                    return true;

            return false;
        }

        // Complete button handler: with every card already face-up the deal is
        // guaranteed solvable, so fly the cards onto the foundations one by one,
        // lowest rank to highest, then finish the game.
        public async UniTask AutoCompleteAsync()
        {
            if (!CanAutoComplete())
                return;

            _gameState.State.Value = State.Win;
            HasStarted.Value = false;

            var delayMs = Mathf.Max(40, (int)(_cardConfig.AnimationDuration * 1000) / 3);

            for (var t = 0; t <= (int)Card.Types.King; t++)
            {
                for (var s = 0; s < PileFoundations.Count && s < 4; s++)
                {
                    var card = FindCard((Card.Suits)s, (Card.Types)t);
                    if (card == null)
                        continue;

                    card.IsInteractable.Value = false;
                    card.IsVisible.Value = true;
                    if (!card.IsFaceUp.Value)
                        card.Flip();

                    // Score each auto-placed card the same as a manual move to a
                    // foundation, with the card's position so the score popup
                    // still flies up. Waste and tableau both award the foundation
                    // value; anything else (e.g. straight from stock) uses the
                    // tableau->foundation value too, so no card is ever free.
                    var source = card.Pile;
                    var pos = card.Position.Value;
                    var points = source != null && source.IsWaste
                        ? _gameConfig.PointsWasteToFoundation
                        : _gameConfig.PointsTableauToFoundation;

                    PileFoundations[s].AddCard(card);
                    _pointsService.Add(points, pos);

                    await UniTask.Delay(delayMs);
                }
            }

            FinishGame();
        }

        public async UniTask TryShowHintAsync()
        {
            // Try to get hint
            var hint = _hintService.GetHint();

            if (hint == null)
                return;

            // Make copies of the original card and all cards above it
            var pile = hint.Pile;
            var cardsToCopy = hint.Card.Pile.SplitAt(hint.Card);
            var copies = _cardSpawner.MakeCopies(cardsToCopy);

            // Initialize copies without adding them to the pile
            for (var i = 0; i < copies.Count; i++)
            {
                // Calculate order
                var copy = copies[i];
                var index = pile.Cards.Count + i;
                copy.Card.Order.Value = index;

                // Calculate position
                var count = pile.Cards.Count + 1 + i;
                var prevCard =
                    i > 0
                        ? copies[i - 1].Card
                        : pile.HasCards
                            ? pile.Cards[index - 1]
                            : null;
                copy.Card.Position.Value = pile.CalculateCardPosition(index, count, prevCard);
            }

            _audioService.PlaySfx(Audio.SfxHint, 0.5f);

            // Wait until the animation completes
            var delayMs = (int)(_cardConfig.AnimationDuration * 1000) + 50;
            await UniTask.Delay(delayMs);

            // Despawn copies
            for (var i = 0; i < copies.Count; i++)
                _cardSpawner.Despawn(copies[i]);

            await UniTask.Delay(delayMs);
        }

        private void Reset()
        {
            // Reset piles
            PileStock.Reset();
            PileWaste.Reset();

            for (var i = 0; i < PileFoundations.Count; i++)
                PileFoundations[i].Reset();

            for (var i = 0; i < PileTableaus.Count; i++)
                PileTableaus[i].Reset();

            // Reset cards
            for (var i = 0; i < Cards.Count; i++)
                Cards[i].Reset(PileStock.Position);

            // Reset services
            _movesService.Reset();
            _pointsService.Reset();
            _commandService.Reset();

            HasStarted.Value = true;
            _gamePopup.State.Value = Popup.None;
        }

        private void ShuffleCards()
        {
            for (var i = Cards.Count - 1; i > 0; i--)
            {
                var n = Random.Range(0, i + 1);
                (Cards[i], Cards[n]) = (Cards[n], Cards[i]);
            }
        }

        // Only accept a deal after the in-memory Klondike search completes all
        // four foundations. Opening-layout guesses are not used.
        private async UniTask ShuffleSolvableAsync(int attempts)
        {
            var original = new List<Card>(Cards);
            List<Card> best = null;
            SolvableDealSearch.Result bestResult = null;
            var solvedCandidates = 0;

            for (var a = 0; a < attempts; a++)
            {
                for (var i = 0; i < original.Count; i++) Cards[i] = original[i];
                ShuffleCards();

                var candidate = new List<Card>(Cards);
                var drawThree = _options.DrawThree.Value;
                var result = await UniTask.RunOnThreadPool(
                    () => SolvableDealSearch.Solve(candidate, drawThree, DealSearchStateLimit)
                );
                if (!result.IsSolved)
                    continue;

                solvedCandidates++;
                if (result.IsEasierThan(bestResult))
                {
                    bestResult = result;
                    best = candidate;
                }
                if (solvedCandidates >= VerifiedDealCandidates)
                    break;
            }

            // A state cap may reject a solvable deal, but a deal that reaches
            // this list is always proven. Never fall back to an unverified
            // shuffle just because the first batch was difficult to search.
            while (best == null)
            {
                for (var i = 0; i < original.Count; i++) Cards[i] = original[i];
                ShuffleCards();
                var candidate = new List<Card>(Cards);
                var drawThree = _options.DrawThree.Value;
                var result = await UniTask.RunOnThreadPool(
                    () => SolvableDealSearch.Solve(candidate, drawThree, EmergencyDealSearchStateLimit)
                );
                if (result.IsSolved)
                    best = candidate;
            }

            for (var i = 0; i < best.Count; i++) Cards[i] = best[i];
        }

        private static bool IsOppositeColor(Card a, Card b)
        {
            return (int)a.Suit / 2 != (int)b.Suit / 2;
        }

        private async UniTask DealAsync()
        {
            // Start dealing
            _gameState.State.Value = State.Dealing;
            var delayMs = (int)(_cardConfig.AnimationDuration * 1000) + 50;

            _audioService.PlaySfx(Audio.SfxShuffle, 0.5f);
            await UniTask.Delay(delayMs);

            // Add cards to the stock pile
            PileStock.AddCards(Cards);

            _audioService.PlaySfx(Audio.SfxDeal, 1.0f);
            await UniTask.Delay(delayMs);

            // Deal cards to the Tableau piles
            for (var i = 0; i < PileTableaus.Count; i++)
            for (var j = 0; j < i + 1; j++)
            {
                var topCard = PileStock.TopCard();
                PileTableaus[i].AddCard(topCard);

                if (j == i)
                    topCard.Flip();

                await UniTask.DelayFrame(3);
            }

            // Start playing
            _gameState.State.Value = State.Playing;
        }

        private async UniTask WinAsync()
        {
            // Start win sequence
            _gameState.State.Value = State.Win;
            HasStarted.Value = false;

            int cardsInTableaus;

            do
            {
                cardsInTableaus = 0;

                // Check each tableau pile
                for (var i = 0; i < PileTableaus.Count; i++)
                {
                    var pileTableau = PileTableaus[i];
                    var topCard = pileTableau.TopCard();
                    cardsInTableaus += pileTableau.Cards.Count;

                    // Skip empty pile
                    if (topCard == null)
                        continue;

                    // Skip card that cannot be moved to a foundation pile
                    var pileFoundation = _hintService.CheckPilesForMove(PileFoundations, topCard);

                    if (pileFoundation == null)
                        continue;

                    // Move card to the foundation
                    MoveCard(topCard, pileFoundation);
                    await UniTask.DelayFrame(3);
                }
            } while (cardsInTableaus > 0);

            FinishGame();
        }

        // End-of-game bookkeeping shared by the real win, the instant test win and
        // the animated test auto-complete: banks points, advances the level and
        // raises GameEnded so the UI can run its celebration/reward flow.
        private void FinishGame()
        {
            AddPointsAndSaveLeaderboard();

            var points = _pointsService.Points.Value;
            var moves = _movesService.Moves.Value;
            var timeSeconds = _timerService.ElapsedSeconds;

            // Winning keeps the combo alive across the post-win "New Game";
            // abandoning a game (restart / new deal before winning) breaks it.
            _currentGameWon = true;

            if (!string.IsNullOrEmpty(ActiveDailyKey))
                DailyGameWon.OnNext(ActiveDailyKey);

            var levelResult = _levelService.AddCompletedGame(points, moves, timeSeconds);
            GameEnded.OnNext(new GameEndResult
            {
                LeveledUp = levelResult.LeveledUp,
                IsFirstLevelUp = levelResult.IsFirstLevelUp,
                Level = levelResult.Level,
                ExperienceGained = levelResult.ExperienceGained,
                BaseXp = levelResult.BaseXp,
                ComboXp = levelResult.ComboXp,
                ComboCount = levelResult.ComboCount,
                BestScoreXp = levelResult.BestScoreXp,
                NewBestScore = levelResult.NewBestScore,
                Points = points,
                Moves = moves,
                TimeSeconds = timeSeconds,
                BestScore = levelResult.BestScore,
                BestTimeSeconds = levelResult.BestTimeSeconds,
                BestMoves = levelResult.BestMoves
            });
        }

        // TEST ONLY: flip every card up and fly them onto the foundations by suit,
        // then finish the game. Lets you exercise the whole end-of-game flow
        // (celebration + level-up reward) from any board state.
        public async UniTask ForceCompleteForTestAsync()
        {
            if (_gameState.State.Value == State.Win)
                return;

            _gameState.State.Value = State.Win;
            HasStarted.Value = false;

            for (var s = 0; s < PileFoundations.Count && s < 4; s++)
            {
                var foundation = PileFoundations[s];
                for (var t = 0; t <= (int)Card.Types.King; t++)
                {
                    var card = FindCard((Card.Suits)s, (Card.Types)t);
                    if (card == null)
                        continue;

                    if (!card.IsFaceUp.Value)
                        card.Flip();
                    card.IsVisible.Value = true;
                    card.IsInteractable.Value = false;
                    foundation.AddCard(card);

                    await UniTask.DelayFrame(2);
                }
            }

            FinishGame();
        }

        private Card FindCard(Card.Suits suit, Card.Types type)
        {
            for (var i = 0; i < Cards.Count; i++)
                if (Cards[i].Suit == suit && Cards[i].Type == type)
                    return Cards[i];

            return null;
        }

        // Whether the current deal has been won. Drives whether starting a fresh
        // deal keeps or breaks the consecutive-win combo.
        private bool _currentGameWon;

        private void BeginNewDeal()
        {
            // Leaving a deal we never won breaks the combo; leaving one we won
            // (to play the next game) keeps it going.
            if (!_currentGameWon)
                _levelService.ResetCombo();
            _currentGameWon = false;
            ActiveDailyKey = _pendingDailyKey;
            _pendingDailyKey = null;
        }

        private void Restart()
        {
            BeginNewDeal();
            Reset();
            DealAsync().Forget();
        }

        private const int NewMatchShuffleAttempts = 12;
        private const int VerifiedDealCandidates = 3;
        private const int DealSearchStateLimit = 40000;
        private const int EmergencyDealSearchStateLimit = 160000;

        private async UniTask NewMatchAsync()
        {
            BeginNewDeal();
            Reset();
            if (_pendingDailySeed.HasValue)
            {
                Random.InitState(_pendingDailySeed.Value);
                _pendingDailySeed = null;
            }

            await ShuffleSolvableAsync(NewMatchShuffleAttempts);
            DealAsync().Forget();
        }

        private void Continue()
        {
            _gameState.State.Value = State.Playing;
            _gamePopup.State.Value = Popup.None;
        }

        private void SpawnCards()
        {
            _cardSpawner.SpawnAll();
            Cards = _cardSpawner.Cards.Select(c => c.Card).ToList();
        }

        private void AddPointsAndSaveLeaderboard()
        {
            var item = new Leaderboard.Item
            {
                Points = _pointsService.Points.Value,
                Date = DateTime.Now.ToString("HH:mm MM/dd/yyyy")
            };

            _leaderboard.Add(item);
            _leaderboard.Save();
        }

        private void LoadLeaderboard()
        {
            _leaderboard.Load();
        }

        public class GameEndResult
        {
            public bool LeveledUp;
            public bool IsFirstLevelUp;
            public int Level;
            public int ExperienceGained;

            // XP breakdown, for the sequenced level-bar reveal.
            public int BaseXp;
            public int ComboXp;
            public int ComboCount;
            public int BestScoreXp;
            public bool NewBestScore;

            // This game's stats + the all-time records, for the results table.
            public int Points;
            public int Moves;
            public int TimeSeconds;
            public int BestScore;
            public int BestTimeSeconds;
            public int BestMoves;
        }

        [Serializable]
        public class Config
        {
            public int PointsWasteToTableau = 5;
            public int PointsWasteToFoundation = 10;
            public int PointsTableauToFoundation = 10;
            public int PointsTurnOverTableauCard = 5;
            public int PointsFoundationToTableau = -15;
            public int PointsRecycleWaste = -100;
        }
    

private void SetCardsDimmed(bool dimmed)
        {
            if (Cards == null)
                return;

            var amount = dimmed ? _magicWandVfx.CardDimAmount : 0f;
            for (var i = 0; i < Cards.Count; i++)
                Cards[i].Dim.Value = amount;
        }


public void ForceWinForTest()
        {
            if (_gameState.State.Value == State.Win)
                return;

            _gameState.State.Value = State.Win;
            HasStarted.Value = false;
            FinishGame();
        }
}
}

