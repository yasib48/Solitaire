using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    public class CoinFlyRewardPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform _spawnRoot;
        [SerializeField] private RectTransform _target;
        [SerializeField] private Image _coinTemplate;
        [SerializeField] private int _maxCoins = 8;
        [SerializeField] private float _scatterRadius = 95f;
        [SerializeField] private float _scatterSeconds = 0.22f;
        [SerializeField] private float _flySeconds = 0.46f;
        [SerializeField] private float _coinDelaySeconds = 0.045f;

        private Canvas _canvas;
        private Camera _canvasCamera;
        private Vector3 _targetBaseScale = Vector3.one;
        private bool _cachedScale;

        public void Configure(RectTransform spawnRoot, RectTransform target, Image coinTemplate)
        {
            _spawnRoot = spawnRoot;
            _target = target;
            _coinTemplate = coinTemplate;
            _canvas = GetComponentInParent<Canvas>();
            _canvasCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            CacheTargetScale();
        }

        public async UniTask PlayAsync(int amount)
        {
            if (amount <= 0 || _target == null || _coinTemplate == null)
                return;

            if (_spawnRoot == null)
                _spawnRoot = transform as RectTransform;

            CacheTargetScale();

            var count = Mathf.Clamp(amount <= 5 ? amount : Mathf.CeilToInt(Mathf.Sqrt(amount)) + 2, 1, _maxCoins);
            var tasks = new UniTask[count];
            for (var i = 0; i < count; i++)
                tasks[i] = PlayCoinAsync(i, count);

            await UniTask.WhenAll(tasks);
            PunchTarget();
        }

        private async UniTask PlayCoinAsync(int index, int count)
        {
            var coin = CreateCoin();
            if (coin == null)
                return;

            var rect = coin.rectTransform;
            var start = GetCanvasCenter();
            var angle = count == 1 ? 90f : (360f / count) * index + 12f;
            var offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * _scatterRadius;
            var scattered = start + (Vector3)offset;
            var target = GetTargetPosition();

            rect.anchoredPosition = start;
            rect.localScale = Vector3.zero;
            coin.color = WithAlpha(coin.color, 0f);

            var sequence = DOTween.Sequence();
            sequence.AppendInterval(index * _coinDelaySeconds);
            sequence.Append(coin.DOFade(1f, 0.08f));
            sequence.Join(rect.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack));
            sequence.Join(rect.DOAnchorPos(scattered, _scatterSeconds).SetEase(Ease.OutQuad));
            sequence.Append(rect.DOAnchorPos(target, _flySeconds).SetEase(Ease.InOutQuad));
            sequence.Join(rect.DOScale(new Vector3(0.35f, 0.35f, 1f), _flySeconds).SetEase(Ease.InQuad));
            sequence.Join(coin.DOFade(0.2f, _flySeconds).SetEase(Ease.InQuad));

            await sequence.AsyncWaitForCompletion();
            if (coin != null)
                Destroy(coin.gameObject);
        }

        private Image CreateCoin()
        {
            var parent = _spawnRoot != null ? _spawnRoot : transform as RectTransform;
            if (parent == null)
                return null;

            var coin = Instantiate(_coinTemplate, parent);
            coin.gameObject.SetActive(true);
            coin.raycastTarget = false;
            coin.transform.SetAsLastSibling();
            coin.sprite = _coinTemplate.sprite;
            coin.preserveAspect = _coinTemplate.preserveAspect;
            coin.rectTransform.sizeDelta = _coinTemplate.rectTransform.rect.size.sqrMagnitude > 0.01f
                ? _coinTemplate.rectTransform.rect.size
                : new Vector2(72f, 72f);

            // The template's anchors are inherited from its original (off-center) parent.
            // All flight math here assumes anchoredPosition is measured from the spawn root's center.
            coin.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            coin.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            coin.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            return coin;
        }

        private Vector3 GetCanvasCenter()
        {
            var rect = _spawnRoot != null ? _spawnRoot : transform as RectTransform;
            return rect != null ? rect.rect.center : Vector3.zero;
        }

        private Vector3 GetTargetPosition()
        {
            if (_spawnRoot == null || _target == null)
                return Vector3.zero;

            var screenPoint = RectTransformUtility.WorldToScreenPoint(_canvasCamera, _target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_spawnRoot, screenPoint, _canvasCamera, out var localPoint);
            return localPoint;
        }

        private void PunchTarget()
        {
            if (_target == null)
                return;

            _target.DOKill();
            _target.localScale = _targetBaseScale;
            _target.DOPunchScale(new Vector3(0.14f, 0.14f, 0f), 0.18f, 8, 0.7f)
                .OnComplete(() => _target.localScale = _targetBaseScale);
        }

        private void CacheTargetScale()
        {
            if (_cachedScale || _target == null)
                return;

            _targetBaseScale = _target.localScale;
            _cachedScale = true;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}