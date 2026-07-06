using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Reusable open/close controller for a UI panel that slides in from an
    ///     offset above its resting position and fades in, then slides back out
    ///     and fades on close.
    ///
    ///     Designed to be safe under the conditions that were breaking the old
    ///     bespoke per-panel tween code:
    ///     - Every call reads the panel's *current* live position as its tween
    ///       start, so redirecting mid-flight (Open while Closing, or vice
    ///       versa) reverses smoothly instead of snapping to a hardcoded pose.
    ///     - Calls are idempotent: OpenAsync while already open, or CloseAsync
    ///       while already closed, is a cheap no-op. This makes it safe to call
    ///       from a per-frame reconciliation loop (e.g. LateUpdate) without
    ///       fighting an in-flight animation.
    ///     - A superseded call (one that got interrupted by a newer Open/Close)
    ///       resolves immediately instead of hanging forever, so callers never
    ///       get stuck awaiting a tween that was cancelled out from under them.
    ///     - Initialization is lazy rather than tied to Awake/Start, so it works
    ///       correctly even when the GameObject starts inactive in the scene
    ///       (Unity never calls Awake on a still-inactive object).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiSlidePanel : MonoBehaviour
    {
        private enum State
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        [SerializeField] private Vector2 _closedOffset = new Vector2(0f, 170f);
        [SerializeField] private float _openDuration = 0.24f;
        [SerializeField] private float _closeDuration = 0.18f;
        [SerializeField] private Ease _openEase = Ease.OutCubic;
        [SerializeField] private Ease _closeEase = Ease.InCubic;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private bool _cachedRest;
        private State _state = State.Closed;
        private int _generation;

        /// <summary>True while open or mid-open. Safe to poll every frame.</summary>
        public bool IsOpen => _state == State.Open || _state == State.Opening;

        private void EnsureInit()
        {
            if (_rect != null)
                return;

            _rect = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();

            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void CacheRestPosition()
        {
            EnsureInit();
            if (_cachedRest || _rect == null)
                return;

            _restPosition = _rect.anchoredPosition;
            _cachedRest = true;
        }

        public async UniTask OpenAsync()
        {
            EnsureInit();
            if (_rect == null)
                return;

            if (_state == State.Open)
                return;

            CacheRestPosition();

            var wasClosed = _state == State.Closed;
            var myGeneration = ++_generation;
            _state = State.Opening;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            _rect.DOKill();
            _group.DOKill();

            if (wasClosed)
            {
                // Coming from a fully-closed, inactive state: snap to the
                // off-screen pose first so the open tween has something to
                // slide down from, instead of a same-position no-op.
                _rect.anchoredPosition = _restPosition + _closedOffset;
                _group.alpha = 0f;
            }

            var done = false;
            _rect.DOAnchorPos(_restPosition, _openDuration).SetEase(_openEase).SetUpdate(true);
            _group.DOFade(1f, _openDuration * 0.8f).SetUpdate(true).OnComplete(() => done = true);

            await UniTask.WaitUntil(() => done || myGeneration != _generation);

            if (myGeneration != _generation)
                return; // A newer Open/Close call superseded us; it owns the final state.

            _state = State.Open;
        }

        public async UniTask CloseAsync()
        {
            EnsureInit();
            if (_rect == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_state == State.Closed)
                return;

            CacheRestPosition();

            var myGeneration = ++_generation;
            _state = State.Closing;

            _rect.DOKill();
            _group.DOKill();

            var done = false;
            _rect.DOAnchorPos(_restPosition + _closedOffset, _closeDuration).SetEase(_closeEase).SetUpdate(true);
            _group.DOFade(0f, _closeDuration).SetUpdate(true).OnComplete(() => done = true);

            await UniTask.WaitUntil(() => done || myGeneration != _generation);

            if (myGeneration != _generation)
                return; // A newer Open/Close call superseded us; it owns the final state.

            _state = State.Closed;
            gameObject.SetActive(false);
        }

        /// <summary>
        ///     Presents the panel already fully open, with no slide, for callers
        ///     (like a multi-step reveal sequence) that want to run their own
        ///     animation on top of an already-settled panel.
        /// </summary>
        public void SnapOpen()
        {
            CacheRestPosition();
            ++_generation;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            _rect.DOKill();
            _group.DOKill();
            _rect.anchoredPosition = _restPosition;
            _group.alpha = 1f;
            _state = State.Open;
        }
    }
}
