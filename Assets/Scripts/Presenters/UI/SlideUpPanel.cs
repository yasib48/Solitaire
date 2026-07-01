using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Slides this panel up from below the screen whenever it becomes active,
    ///     and back down (then deactivates) when <see cref="Hide" /> is called.
    ///     Put it on the panel that should move. The resting position is wherever
    ///     you place the panel in the editor. Uses DOTween and unscaled time so it
    ///     still animates while the game is paused.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SlideUpPanel : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.35f;
        [SerializeField] private Ease _showEase = Ease.OutCubic;
        [SerializeField] private Ease _hideEase = Ease.InCubic;

        [Tooltip("Object disabled after the hide animation. Leave empty to use this panel's parent (e.g. the full-screen \"Play\" panel).")]
        [SerializeField] private GameObject _closeTarget;

        private RectTransform _rect;
        private float _shownY;
        private float _hiddenY;
        private Tweener _tween;
        private bool _initialized;

        private void Awake()
        {
            Init();
        }

        private void OnEnable()
        {
            Init();
            Show();
        }

        public void Show()
        {
            _tween?.Kill();
            SetY(_hiddenY);
            _tween = _rect.DOAnchorPosY(_shownY, _duration).SetEase(_showEase).SetUpdate(true);
        }

        public void Hide()
        {
            _tween?.Kill();
            var target = _closeTarget != null
                ? _closeTarget
                : (transform.parent != null ? transform.parent.gameObject : gameObject);

            _tween = _rect.DOAnchorPosY(_hiddenY, _duration)
                .SetEase(_hideEase)
                .SetUpdate(true)
                .OnComplete(() => target.SetActive(false));
        }

        private void Init()
        {
            if (_initialized)
                return;

            _rect = (RectTransform)transform;
            _shownY = _rect.anchoredPosition.y;

            // Travel far enough to sit fully below the visible area.
            var parent = _rect.parent as RectTransform;
            var travel = parent != null && parent.rect.height > 0 ? parent.rect.height : Screen.height;
            _hiddenY = _shownY - travel;

            _initialized = true;
        }

        private void SetY(float y)
        {
            var p = _rect.anchoredPosition;
            p.y = y;
            _rect.anchoredPosition = p;
        }
    }
}
