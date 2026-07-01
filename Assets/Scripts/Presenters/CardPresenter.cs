using System;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class CardPresenter
        : MonoBehaviour,
            IPoolable<Card.Suits, Card.Types, IMemoryPool>,
            IDisposable,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            IPointerClickHandler
    {
        private const float MoveEpsilon = 0.00001f;
        private const int AnimOrder = 100;

        [Header("Sprites")]
        [SerializeField]
        private SpriteRenderer _back;

        [SerializeField]
        private SpriteRenderer _front;

        [SerializeField]
        private SpriteRenderer _type;

        [SerializeField]
        private SpriteRenderer _suit1;

        [SerializeField]
        private SpriteRenderer _suit2;

        [Header("Foundation Particle")]
        [SerializeField]
        private ParticleSystem _foundationVfxPrefab;

        [SerializeField]
        private Material[] _suitParticleMaterials;

        [Inject]
        private readonly Card _card;

        [Inject]
        private readonly Card.Config _config;

        [Inject]
        private readonly IDragAndDropHandler _dndHandler;

        [Inject]
        private readonly Game _game;
        private BoxCollider2D _collider;
        private IMemoryPool _pool;
        private Transform _transform;
        private Tweener _tweenMove;

        private Tweener _tweenScale;
        private Sequence _tweenShake;

        // The card's authored scale (prefab), restored after a flip so face-up
        // cards don't snap back to 1 and end up smaller than face-down ones.
        private Vector3 _baseScale = Vector3.one;

        public Card Card => _card;

        private void Start()
        {
            _collider = GetComponent<BoxCollider2D>();
            _transform = transform;
            _baseScale = _transform.localScale;

            _card.Alpha.Subscribe(UpdateAlpha).AddTo(this);
            _card.Order.Subscribe(UpdateOrder).AddTo(this);
            _card.IsVisible.Subscribe(UpdateVisiblity).AddTo(this);
            _card.IsInteractable.Subscribe(UpdateInteractability).AddTo(this);
            _card.IsFaceUp.Where(CanFlip).Subscribe(AnimateFlip).AddTo(this);
            _card.Position.Where(CanMove).Subscribe(AnimateMove).AddTo(this);
        }

        #region IDisposable

        public void Dispose()
        {
            _pool.Despawn(this);
        }

        #endregion IDisposable

        private bool CanFlip(bool isFaceUp)
        {
            return (isFaceUp && !_front.gameObject.activeSelf)
                || (!isFaceUp && !_back.gameObject.activeSelf);
        }

        private void AnimateFlip(bool isFaceUp)
        {
            // Scale X from 1 to 0 then back to 1 again,
            // switching between front and back sprites in the middle.
            // This gives the illusion of flipping the card in 2D.
            if (_tweenScale == null)
                _tweenScale = _transform
                    .DOScaleX(0f, _config.AnimationDuration / 2f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false)
                    .OnStepComplete(() => Flip(_card.IsFaceUp.Value))
                    .OnComplete(() => _transform.localScale = _baseScale);
            else
                _tweenScale.Restart();
        }

        private bool CanMove(Vector3 position)
        {
            return Vector3.SqrMagnitude(position - _transform.position) > MoveEpsilon;
        }

        private void AnimateMove(Vector3 position)
        {
            if (_card.IsDragged)
            {
                // Update position instantly while the card is being dragged
                _transform.position = position;
            }
            else
            {
                // Move card over time to the target position while changing
                // order at the start and end so the cards are overlaid correctly.
                if (_tweenMove == null)
                    _tweenMove = _transform
                        .DOLocalMove(position, _config.AnimationDuration)
                        .SetEase(Ease.OutQuad)
                        .SetAutoKill(false)
                        .OnRewind(() =>
                        {
                            _card.OrderToRestore = _card.IsInPile
                                ? _card.Pile.Cards.IndexOf(_card)
                                : _card.Order.Value;
                            _card.Order.Value = AnimOrder + _card.OrderToRestore;
                        })
                        .OnComplete(() =>
                        {
                            _card.Order.Value = _card.OrderToRestore;
                            TryPlayFoundationVfx();
                        });
                else
                    _tweenMove.ChangeEndValue(position, true).Restart();
            }
        }

        private void TryPlayFoundationVfx()
        {
            if (_foundationVfxPrefab == null || _suitParticleMaterials == null)
                return;
            if (!_card.IsInPile || !_card.Pile.IsFoundation)
                return;

            var vfx = Instantiate(_foundationVfxPrefab, _transform.position, Quaternion.identity);
            var idx = (int)_card.Suit;
            if (idx < _suitParticleMaterials.Length)
            {
                var renderer = vfx.GetComponent<ParticleSystemRenderer>();
                renderer.material = _suitParticleMaterials[idx];
            }
            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetime.constantMax);
        }

        private void UpdateOrder(int order)
        {
            var sortingOrder = order * 10;
            _back.sortingOrder = sortingOrder;
            _front.sortingOrder = sortingOrder;
            if (_type != null) _type.sortingOrder = sortingOrder + 1;
            if (_suit1 != null) _suit1.sortingOrder = sortingOrder + 1;
            if (_suit2 != null) _suit2.sortingOrder = sortingOrder + 1;
        }

        private void UpdateAlpha(float alpha)
        {
            var color = _back.color;
            color.a = alpha;
            _back.color = color;

            color = _front.color;
            color.a = alpha;
            _front.color = color;

            if (_type != null) { color = _type.color; color.a = alpha; _type.color = color; }
            if (_suit1 != null) { color = _suit1.color; color.a = alpha; _suit1.color = color; }
            if (_suit2 != null) { color = _suit2.color; color.a = alpha; _suit2.color = color; }
        }

        private void UpdateVisiblity(bool isVisible)
        {
            _back.enabled = isVisible;
            _front.enabled = isVisible;
            if (_type != null) _type.enabled = isVisible;
            if (_suit1 != null) _suit1.enabled = isVisible;
            if (_suit2 != null) _suit2.enabled = isVisible;
        }

        private void UpdateInteractability(bool isInteractable)
        {
            _collider.enabled = isInteractable;
        }

        private void Initialize()
        {
            name = $"Card_{_card.Suit}_{_card.Type}";

            if (_config.Theme != null)
            {
                _front.sprite = _config.Theme.GetCard(_card.Suit, _card.Type);
                _back.sprite = _config.Theme.Back;
                if (_type != null) { _type.gameObject.SetActive(false); _type = null; }
                if (_suit1 != null) { _suit1.gameObject.SetActive(false); _suit1 = null; }
                if (_suit2 != null) { _suit2.gameObject.SetActive(false); _suit2 = null; }
            }
            else
            {
                var spriteSuit = _config.SuitSprites[(int)_card.Suit];
                _suit1.sprite = spriteSuit;
                _suit2.sprite = spriteSuit;

                var color = _config.Colors[(int)_card.Suit];
                var spriteType = _config.TypeSprites[(int)_card.Type];
                _type.sprite = spriteType;
                _type.color = color;
            }
        }

        public void Flip(bool isFaceUp)
        {
            _back.gameObject.SetActive(!isFaceUp);
            _front.gameObject.SetActive(isFaceUp);
        }

        private void Shake()
        {
            // Tiny, snappy one-right-one-left wiggle that always snaps back home.
            if (_tweenShake != null && _tweenShake.IsActive() && _tweenShake.IsPlaying())
                return;

            var origin = _transform.localPosition;
            var dx = new Vector3(0.1f, 0f, 0f);

            _tweenShake = DOTween.Sequence();
            _tweenShake
                .Append(_transform.DOLocalMove(origin + dx, 0.05f).SetEase(Ease.OutQuad))
                .Append(_transform.DOLocalMove(origin - dx, 0.05f).SetEase(Ease.InOutQuad))
                .Append(_transform.DOLocalMove(origin, 0.05f).SetEase(Ease.InQuad))
                .OnComplete(() => _transform.localPosition = origin);
        }

        // ponytail: ~30ms light tap, Android only
        private static void HapticLight()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator.Call("vibrate", 30L);
            }
            catch { }
#endif
        }

        public class Factory : PlaceholderFactory<Card.Suits, Card.Types, CardPresenter> { }

        #region IEventSystemHandlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_card.IsMoveable)
            {
                _dndHandler.BeginDrag(eventData, _card.Pile.SplitAt(_card));
                _card.IsInteractable.Value = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_card.IsMoveable)
                _dndHandler.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_card.IsMoveable)
                return;

            // The drop target is decided by where the dragged card overlaps another
            // card/pile, not where the finger is. So grabbing a card by its very
            // corner still works as long as the card itself reaches the target.
            var target = FindOverlapPile();

            if (target != null)
            {
                _dndHandler.Drop();
                _game.MoveCard(_card, target);
            }

            _dndHandler.EndDrag();
            _card.IsInteractable.Value = true;
        }

        // Drop is resolved by the dragged card in OnEndDrag, so the pointer-based
        // drop on a target is intentionally a no-op.
        public void OnDrop(PointerEventData eventData) { }

        private Pile FindOverlapPile()
        {
            var size = Vector2.Scale(_collider.size, transform.lossyScale);
            var hits = Physics2D.OverlapBoxAll(transform.position, size, 0f);

            Pile best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                Pile pile = null;

                if (hit.TryGetComponent(out CardPresenter card))
                {
                    if (card.Card.IsDragged)
                        continue; // ignore the cards we're dragging
                    pile = card.Card.Pile;
                }
                else if (hit.TryGetComponent(out PilePresenter pilePresenter))
                {
                    pile = pilePresenter.Pile;
                }

                if (pile == null || pile == _card.Pile || !pile.CanAddCard(_card))
                    continue;

                // Among valid overlaps, prefer the closest one to the card's center
                var distance = (
                    (Vector2)hit.transform.position - (Vector2)transform.position
                ).sqrMagnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pile;
                }
            }

            return best;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            // Single tap: draw from stock. Otherwise any face-up, moveable card
            // responds — including a card with a valid run on top of it (the whole
            // run moves). Auto-move to the most sensible pile (foundation first,
            // then a valid tableau), or shake if it can't go anywhere. Face-down
            // cards aren't moveable, so they ignore the tap entirely.
            if (_card.IsDrawable)
            {
                _game.DrawCard();
            }
            else if (_card.IsMoveable)
            {
                if (!_game.MoveCard(_card, null))
                {
                    _game.PlayErrorSfx();
                    Shake();
                    HapticLight();
                }
            }
        }

        #endregion IEventSystemHandlers

        #region IPoolable

        public void OnSpawned(Card.Suits suit, Card.Types type, IMemoryPool pool)
        {
            // Init model
            _pool = pool;
            _card.Init(suit, type);
            Initialize();
        }

        public void OnDespawned()
        {
            // Reset model
            _pool = null;
            _card.Reset(Vector3.zero);
        }

        #endregion IPoolable
    }
}
