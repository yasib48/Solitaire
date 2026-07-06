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
        private SpriteRenderer _dimOverlay;
        private SpriteRenderer _highlightOverlay;// The card's authored scale (prefab), restored after a flip so face-up
        // cards don't snap back to 1 and end up smaller than face-down ones.
        private Vector3 _baseScale = Vector3.one;

        public Card Card => _card;

private void Start()
        {
            _collider = GetComponent<BoxCollider2D>();
            _transform = transform;
            _baseScale = _transform.localScale;
            CreateDimOverlay();
            CreateHighlightOverlay();

            _card.Alpha.Subscribe(UpdateAlpha).AddTo(this);
            _card.Dim.Subscribe(UpdateDim).AddTo(this);
            _card.Highlight.Subscribe(UpdateHighlight).AddTo(this);
            _card.Order.Subscribe(UpdateOrder).AddTo(this);
            _card.FrontOrderOverride.Subscribe(UpdateFrontOrderOverride).AddTo(this);
            _card.IsVisible.Subscribe(UpdateVisiblity).AddTo(this);
            _card.IsInteractable.Subscribe(UpdateInteractability).AddTo(this);
            _card.IsFaceUp.Where(CanFlip).Subscribe(AnimateFlip).AddTo(this);
            _card.Position.Where(CanMove).Subscribe(AnimateMove).AddTo(this);
            _card.Scale.Subscribe(AnimateScale).AddTo(this);
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
                    .OnComplete(() => _transform.localScale = _baseScale * _card.Scale.Value);
            else
                _tweenScale.Restart();
        }

        private Tweener _tweenScaleMul;

        // Smoothly grow/shrink the whole card by the model's Scale multiplier.
        // Used by the magic wand to lift the revealed card for its centre reveal.
        private void AnimateScale(float scale)
        {
            var target = _baseScale * scale;

            if (Mathf.Approximately((_transform.localScale - target).sqrMagnitude, 0f))
            {
                _transform.localScale = target;
                return;
            }

            if (_tweenScaleMul == null)
                _tweenScaleMul = _transform
                    .DOScale(target, _config.AnimationDuration)
                    .SetEase(Ease.OutBack)
                    .SetAutoKill(false);
            else
                _tweenScaleMul.ChangeEndValue(target, true).Restart();
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
            ApplyFrontSortingOrder();
            if (_type != null) _type.sortingOrder = sortingOrder + 1;
            if (_suit1 != null) _suit1.sortingOrder = sortingOrder + 1;
            if (_suit2 != null) _suit2.sortingOrder = sortingOrder + 1;
            if (_dimOverlay != null)
            {
                _dimOverlay.sortingLayerID = _front.sortingLayerID;
                _dimOverlay.sortingOrder = sortingOrder + 8;
            }
            if (_highlightOverlay != null)
            {
                _highlightOverlay.sortingLayerID = _front.sortingLayerID;
                _highlightOverlay.sortingOrder = sortingOrder + 9;
            }
        }

        private int? _frontOrderOverride;

        // Lets a card's front-face sorting order be pinned to an exact value
        // regardless of Order (see Card.FrontOrderOverride) - used by the magic
        // wand's centre reveal so the card stays on top of everything else the
        // whole time it's flying, without needing to touch Order at all (which
        // still drives the pile's own stacking/back-face order as normal).
        private void UpdateFrontOrderOverride(int? order)
        {
            _frontOrderOverride = order;
            ApplyFrontSortingOrder();
        }

        private void ApplyFrontSortingOrder()
        {
            _front.sortingOrder = _frontOrderOverride ?? _card.Order.Value * 10;
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

private void CreateDimOverlay()
        {
            if (_dimOverlay != null)
                return;

            var overlay = new GameObject("Dim Overlay");
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;
            _dimOverlay = overlay.AddComponent<SpriteRenderer>();
            _dimOverlay.color = new Color(0f, 0f, 0f, 0f);
            _dimOverlay.enabled = false;
            UpdateDimSprite();
        }

        private void UpdateDim(float amount)
        {
            if (_dimOverlay == null)
                return;

            UpdateDimSprite();
            amount = Mathf.Clamp01(amount);
            var color = _dimOverlay.color;
            color.a = amount;
            _dimOverlay.color = color;
            _dimOverlay.enabled = _card.IsVisible.Value && amount > 0.01f;
        }

        private void UpdateDimSprite()
        {
            if (_dimOverlay == null)
                return;

            _dimOverlay.sprite = _card.IsFaceUp.Value ? _front.sprite : _back.sprite;
            _dimOverlay.flipX = _card.IsFaceUp.Value ? _front.flipX : _back.flipX;
            _dimOverlay.flipY = _card.IsFaceUp.Value ? _front.flipY : _back.flipY;
            _dimOverlay.sortingLayerID = _front.sortingLayerID;
        }

        private void CreateHighlightOverlay()
        {
            if (_highlightOverlay != null)
                return;

            var overlay = new GameObject("Highlight Overlay");
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;
            _highlightOverlay = overlay.AddComponent<SpriteRenderer>();
            _highlightOverlay.color = new Color(1f, 0.87f, 0.15f, 0f);
            _highlightOverlay.enabled = false;
            UpdateHighlightSprite();
        }

        private void UpdateHighlight(float amount)
        {
            if (_highlightOverlay == null)
                return;

            UpdateHighlightSprite();
            amount = Mathf.Clamp01(amount);
            var color = _highlightOverlay.color;
            color.a = amount * 0.6f;
            _highlightOverlay.color = color;
            _highlightOverlay.enabled = _card.IsVisible.Value && amount > 0.01f;
        }

        private void UpdateHighlightSprite()
        {
            if (_highlightOverlay == null)
                return;

            _highlightOverlay.sprite = _card.IsFaceUp.Value ? _front.sprite : _back.sprite;
            _highlightOverlay.flipX = _card.IsFaceUp.Value ? _front.flipX : _back.flipX;
            _highlightOverlay.flipY = _card.IsFaceUp.Value ? _front.flipY : _back.flipY;
            _highlightOverlay.sortingLayerID = _front.sortingLayerID;
        }


private void UpdateVisiblity(bool isVisible)
        {
            _back.enabled = isVisible;
            _front.enabled = isVisible;
            if (_type != null) _type.enabled = isVisible;
            if (_suit1 != null) _suit1.enabled = isVisible;
            if (_suit2 != null) _suit2.enabled = isVisible;
            UpdateDim(_card.Dim.Value);
            UpdateHighlight(_card.Highlight.Value);
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

            // CardPresenters are pooled (e.g. hint-preview copies are spawned
            // and despawned via the same pool as real cards). Card.Reset()
            // sets Dim back to 0, but a ReactiveProperty only notifies
            // subscribers when the value actually changes - if this pooled
            // slot's last use also ended at Dim 0, UpdateDim never re-fires,
            // so a leftover dim overlay from whatever that slot rendered
            // previously would otherwise carry over onto the new card. Force
            // it in sync explicitly on every spawn instead of relying on the
            // reactive callback.
            UpdateDim(_card.Dim.Value);
            UpdateHighlight(_card.Highlight.Value);
        }

public void Flip(bool isFaceUp)
        {
            _back.gameObject.SetActive(!isFaceUp);
            _front.gameObject.SetActive(isFaceUp);
            UpdateDimSprite();
            UpdateHighlightSprite();
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
