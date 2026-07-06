using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    public class DailyGiftBoxPresenter : MonoBehaviour
    {
        [SerializeField] private GiftBoxSlot[] _boxes = Array.Empty<GiftBoxSlot>();

        public int BoxCount => _boxes?.Length ?? 0;

        private void Awake()
        {
            Refresh(false);
        }

        private void OnValidate()
        {
            Refresh(false);
        }

        public bool IsOpened(int index)
        {
            return TryGetBox(index, out var box) && box.IsOpened;
        }

        public void SetOpened(int index, bool opened, bool animate = false)
        {
            if (!TryGetBox(index, out var box))
                return;

            box.SetOpened(opened, animate);
        }

        public bool TryGetSprites(int index, out Sprite closedSprite, out Sprite openedSprite)
        {
            closedSprite = null;
            openedSprite = null;
            if (!TryGetBox(index, out var box))
                return false;

            closedSprite = box.ClosedSprite;
            openedSprite = box.OpenedSprite;
            return closedSprite != null || openedSprite != null;
        }

        public void SetFreeBoxClaimed(bool claimed, bool animate = false)
        {
            if (_boxes == null)
                return;

            for (var i = 0; i < _boxes.Length; i++)
                _boxes[i]?.SetAdBadgeVisible(i > 0 && claimed, animate);
        }

        public void Refresh(bool animate = false)
        {
            if (_boxes == null)
                return;

            for (var i = 0; i < _boxes.Length; i++)
                _boxes[i]?.Refresh(animate);
        }

        private bool TryGetBox(int index, out GiftBoxSlot box)
        {
            box = null;
            if (_boxes == null || index < 0 || index >= _boxes.Length)
                return false;

            box = _boxes[index];
            return box != null;
        }

        [Serializable]
        private class GiftBoxSlot
        {
            private const float OpenedScaleMultiplier = 1.4f;

            [SerializeField] private Button _button;
            [SerializeField] private Image _boxImage;
            [SerializeField] private Sprite _closedSprite;
            [SerializeField] private Sprite _openedSprite;
            [SerializeField] private GameObject _adBadge;
            [SerializeField] private bool _opened;

            private Vector3 _boxBaseScale;
            private Vector3 _adBadgeBaseScale;
            private bool _hasBoxBaseScale;
            private bool _hasAdBadgeBaseScale;

            public bool IsOpened => _opened;
            public Sprite ClosedSprite => _closedSprite;
            public Sprite OpenedSprite => _openedSprite;

            public void SetOpened(bool opened, bool animate)
            {
                CacheScales();
                if (_opened == opened)
                {
                    Refresh(false);
                    return;
                }

                _opened = opened;
                Refresh(animate);
            }

            public void Refresh(bool animate)
            {
                CacheScales();
                ApplyBoxVisual();
                ApplyBoxScale(animate && _opened);
                ApplyButtonState();
            }

            public void SetAdBadgeVisible(bool visible, bool animate)
            {
                if (_adBadge == null)
                    return;

                CacheScales();
                _adBadge.transform.DOKill();
                var canvasGroup = EnsureCanvasGroup(_adBadge);
                canvasGroup.DOKill();

                if (!visible)
                {
                    _adBadge.SetActive(false);
                    _adBadge.transform.localScale = _adBadgeBaseScale;
                    canvasGroup.alpha = 1f;
                    return;
                }

                _adBadge.SetActive(true);
                if (!animate)
                {
                    _adBadge.transform.localScale = _adBadgeBaseScale;
                    canvasGroup.alpha = 1f;
                    return;
                }

                _adBadge.transform.localScale = ScaleBy(_adBadgeBaseScale, 0.84f);
                canvasGroup.alpha = 0f;
                _adBadge.transform.DOScale(_adBadgeBaseScale, 0.2f).SetEase(Ease.OutBack);
                canvasGroup.DOFade(1f, 0.14f).SetEase(Ease.OutQuad);
            }

            private void ApplyBoxVisual()
            {
                if (_boxImage == null)
                    return;

                var nextSprite = _opened ? _openedSprite : _closedSprite;
                if (nextSprite != null)
                    _boxImage.sprite = nextSprite;
            }

            private void ApplyBoxScale(bool animate)
            {
                var target = GetBoxScaleTarget();
                if (target == null)
                    return;

                var targetScale = _opened ? ScaleBy(_boxBaseScale, OpenedScaleMultiplier) : _boxBaseScale;
                target.DOKill();

                if (!animate)
                {
                    target.localScale = targetScale;
                    return;
                }

                target.localScale = ScaleBy(targetScale, 0.88f);
                target.DOScale(targetScale, 0.22f).SetEase(Ease.OutBack);
            }

            private void ApplyButtonState()
            {
                if (_button == null)
                    return;

                _button.interactable = !_opened;
                _button.enabled = !_opened;
            }

            private Transform GetBoxScaleTarget()
            {
                if (_button != null)
                    return _button.transform;
                return _boxImage != null ? _boxImage.transform : null;
            }

            private void CacheScales()
            {
                var boxScaleTarget = GetBoxScaleTarget();
                if (!_hasBoxBaseScale && boxScaleTarget != null)
                {
                    _boxBaseScale = boxScaleTarget.localScale;
                    _hasBoxBaseScale = true;
                }

                if (!_hasAdBadgeBaseScale && _adBadge != null)
                {
                    _adBadgeBaseScale = _adBadge.transform.localScale;
                    _hasAdBadgeBaseScale = true;
                }
            }

            private static Vector3 ScaleBy(Vector3 baseScale, float multiplier)
            {
                return new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
            }

            private static CanvasGroup EnsureCanvasGroup(GameObject target)
            {
                var canvasGroup = target.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = target.AddComponent<CanvasGroup>();

                return canvasGroup;
            }
        }
    }
}