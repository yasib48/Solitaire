using System;
using Solitaire.Models;
using Solitaire.Services;
using Zenject;

namespace Solitaire.Commands
{
    public class MoveCardCommand : ICommand, IDisposable, IPoolable<Card, Pile, Pile, IMemoryPool>
    {
        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Game.Config _gameConfig;

        [Inject]
        private readonly IPointsService _pointsService;

        private Card _card;
        private Pile _pileSource;
        private Pile _pileTarget;
        private IMemoryPool _pool;

        private bool _wasTopCardFlipped;

        public void Execute()
        {
            if (_pileSource.TopCard() == _card)
            {
                // Single card
                _pileTarget.AddCard(_card);
            }
            else
            {
                // Multiple cards
                var cards = _pileSource.SplitAt(_card);
                _pileSource.RemoveCards(cards);
                _pileTarget.AddCards(cards);
            }

            // Scoring
            var pos = _card.Position.Value;
            if (_pileSource.IsWaste)
            {
                if (_pileTarget.IsTableau)
                    _pointsService.Add(_gameConfig.PointsWasteToTableau, pos);
                else if (_pileTarget.IsFoundation)
                    _pointsService.Add(_gameConfig.PointsWasteToFoundation, pos);
            }
            else if (_pileSource.IsTableau && _pileTarget.IsFoundation)
            {
                _pointsService.Add(_gameConfig.PointsTableauToFoundation, pos);
            }
            else if (_pileSource.IsFoundation && _pileTarget.IsTableau)
            {
                _pointsService.Add(_gameConfig.PointsFoundationToTableau);
            }

            if (_pileTarget.IsFoundation)
            {
                // ponytail: sound based on card count in the pile, not a global counter
                var soundIndex = System.Math.Min(_pileTarget.Cards.Count, 13);
                _audioService.PlaySfx($"Foundation{soundIndex}", 0.5f);
            }
            else
            {
                _audioService.PlaySfx(Audio.SfxDraw, 0.5f);
            }

            // Reveal card below if needed
            var cardBelow = _pileSource.TopCard();

            if (_pileSource.IsTableau && cardBelow != null && !cardBelow.IsFaceUp.Value)
            {
                cardBelow.Flip();
                _wasTopCardFlipped = true;
                _pointsService.Add(_gameConfig.PointsTurnOverTableauCard, cardBelow.Position.Value);
            }
        }

        public void Undo()
        {
            // Hide top card of the source tableau pile
            var cardTop = _pileSource.TopCard();

            if (
                _pileSource.IsTableau
                && _wasTopCardFlipped
                && cardTop != null
                && cardTop.IsFaceUp.Value
            )
            {
                cardTop.Flip();
                _pointsService.Add(-_gameConfig.PointsTurnOverTableauCard);
            }

            // Scoring
            if (_pileSource.IsWaste)
            {
                if (_pileTarget.IsTableau)
                    _pointsService.Add(-_gameConfig.PointsWasteToTableau);
                else if (_pileTarget.IsFoundation)
                    _pointsService.Add(-_gameConfig.PointsWasteToFoundation);
            }
            else if (_pileSource.IsTableau && _pileTarget.IsFoundation)
            {
                _pointsService.Add(-_gameConfig.PointsTableauToFoundation);
            }
            else if (_pileSource.IsFoundation && _pileTarget.IsTableau)
            {
                _pointsService.Add(-_gameConfig.PointsFoundationToTableau);
            }

            if (_pileTarget.TopCard() == _card)
            {
                // Single card
                _pileSource.AddCard(_card);
            }
            else
            {
                // Multiple cards
                var cards = _pileTarget.SplitAt(_card);
                _pileTarget.RemoveCards(cards);
                _pileSource.AddCards(cards);
            }
        }

        public void Dispose()
        {
            _pool.Despawn(this);
        }

        public void OnDespawned()
        {
            _card = null;
            _pileSource = null;
            _pileTarget = null;
            _pool = null;
        }

        public void OnSpawned(Card card, Pile pileSource, Pile pileTarget, IMemoryPool pool)
        {
            _card = card;
            _pileSource = pileSource;
            _pileTarget = pileTarget;
            _pool = pool;
            _wasTopCardFlipped = false;
        }

        public class Factory : PlaceholderFactory<Card, Pile, Pile, MoveCardCommand> { }
    }
}
