using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Gives a button a tactile "press" feel: it shrinks a little while held
    ///     and springs back when released. Purely visual — sits alongside the
    ///     Button's own click handling and doesn't consume the event.
    /// </summary>
    public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _pressScale = 0.9f;
        [SerializeField] private float _downDuration = 0.08f;
        [SerializeField] private float _upDuration = 0.24f;

        private Button _button;
        private Vector3 _baseScale = Vector3.one;
        private bool _cached;
        private Tweener _tween;

        private void Awake()
        {
            Cache();
        }

        private void OnDisable()
        {
            // Snap back so a button re-shown mid-press isn't stuck small.
            _tween?.Kill();
            if (_cached)
                transform.localScale = _baseScale;
        }

        private void Cache()
        {
            if (_cached)
                return;

            _button = GetComponent<Button>();
            _baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            _cached = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable)
                return;

            Cache();
            _tween?.Kill();
            _tween = transform.DOScale(_baseScale * _pressScale, _downDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Cache();
            _tween?.Kill();
            _tween = transform.DOScale(_baseScale, _upDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }
}
