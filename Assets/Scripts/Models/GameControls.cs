using Cysharp.Threading.Tasks;
using Solitaire.Helpers;
using Solitaire.Services;
using UniRx;

namespace Solitaire.Models
{
    public class GameControls : DisposableEntity
    {
        public GameControls(
            Game game,
            GameState gameState,
            ICommandService commandService,
            IMovesService movesService,
            IAudioService audioService,
            IMagicWandService magicWandService
        )
        {
            var isPlayingSource = gameState.State.Select(s => s == Game.State.Playing);

            HomeCommand = new ReactiveCommand(
                gameState.State.Select(s => s == Game.State.Playing || s == Game.State.Win)
            );

            HomeCommand.Subscribe(_ => gameState.State.Value = Game.State.Home).AddTo(this);

            UndoCommand = new ReactiveCommand(
                isPlayingSource.CombineLatest(
                    commandService.CanUndo,
                    (isPlaying, canUndo) => isPlaying && canUndo
                )
            );

            UndoCommand
                .Subscribe(_ =>
                {
                    commandService.Undo();
                    movesService.Increment();
                    audioService.PlaySfx(Audio.SfxUndo, 0.5f);
                })
                .AddTo(this);

            HintCommand = new AsyncReactiveCommand(isPlayingSource);

            HintCommand
                .Subscribe(_ => game.TryShowHintAsync().ToObservable().AsUnitObservable())
                .AddTo(this);

            // Re-evaluate availability after every move (moves count is the trigger),
            // on play-state changes so the button greys out when no hidden card can
            // be revealed, and whenever the charge count changes so it disables once
            // the player runs out.
            var canMagicWand = isPlayingSource
                .CombineLatest(movesService.Moves, (isPlaying, _) => isPlaying)
                .CombineLatest(
                    magicWandService.Count,
                    (isPlaying, count) => isPlaying && count > 0 && game.CanMagicWand()
                );

            MagicWandCommand = new ReactiveCommand(canMagicWand);

            MagicWandCommand
                .Subscribe(_ =>
                {
                    // The command is only enabled when a hidden card can be revealed,
                    // so spend a charge and play the cinematic reveal.
                    if (game.CanMagicWand())
                    {
                        magicWandService.TryUse();
                        audioService.PlaySfx(Audio.SfxMagicWand, 0.5f);
                        game.MagicWandAsync().Forget();
                    }
                    // Bump moves so canMagicWand re-evaluates and the button updates.
                    movesService.Increment();
                })
                .AddTo(this);

            // Re-check on every move (a move can reveal the last hidden card) and
            // on play-state changes. Enabled once all cards are face-up, so the
            // player can one-tap finish instead of placing every card by hand.
            var canAutoComplete = isPlayingSource
                .CombineLatest(movesService.Moves, (isPlaying, _) => isPlaying)
                .Select(isPlaying => isPlaying && game.CanAutoComplete());

            AutoCompleteCommand = new ReactiveCommand(canAutoComplete);

            AutoCompleteCommand
                .Subscribe(_ => game.AutoCompleteAsync().Forget())
                .AddTo(this);
        }

        public ReactiveCommand HomeCommand { get; }
        public ReactiveCommand UndoCommand { get; }
        public AsyncReactiveCommand HintCommand { get; }
        public ReactiveCommand MagicWandCommand { get; }
        public ReactiveCommand AutoCompleteCommand { get; }
    }
}
