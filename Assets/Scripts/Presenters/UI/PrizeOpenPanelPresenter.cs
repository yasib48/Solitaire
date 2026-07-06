using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    public class PrizeOpenPanelPresenter : MonoBehaviour
    {
        [Header("Box")]
        [SerializeField] private Image _boxImage;
        [SerializeField] private Sprite _closedSprite;
        [SerializeField] private Sprite _openedSprite;

        [Header("Reveal")]
        [SerializeField] private GameObject _prizeRoot;
        [SerializeField] private Button _collectButton;

        [Header("Timing")]
        [SerializeField] private float _closedHoldSeconds = 0.28f;
        [SerializeField] private float _boxOpenPopSeconds = 0.2f;
        [SerializeField] private float _prizeDelaySeconds = 0.08f;
        [SerializeField] private float _prizePopSeconds = 0.24f;
        [SerializeField] private float _buttonDelaySeconds = 0.08f;
        [SerializeField] private float _buttonPopSeconds = 0.2f;

        private Vector3 _boxBaseScale = Vector3.one;
        private Vector3 _prizeBaseScale = Vector3.one;
        private Vector3 _buttonBaseScale = Vector3.one;
        private bool _cachedScales;
        private int _sequenceId;

        private void Awake()
        {
            AutoBindMissingReferences();
            CacheScales();
            PrepareClosedState();
        }

        public void ConfigureSprites(Sprite closedSprite, Sprite openedSprite)
        {
            if (closedSprite != null)
                _closedSprite = closedSprite;
            if (openedSprite != null)
                _openedSprite = openedSprite;
        }

        private void OnValidate()
        {
            AutoBindMissingReferences();
            CacheScales();
            if (!Application.isPlaying)
                PrepareClosedState();
        }

        public void PrepareClosedState()
        {
            AutoBindMissingReferences();
            CacheScales();
            _sequenceId++;

            if (_boxImage != null)
            {
                _boxImage.transform.DOKill();
                _boxImage.transform.localScale = _boxBaseScale;
                if (_closedSprite != null)
                    _boxImage.sprite = _closedSprite;
            }

            SetRevealVisible(_prizeRoot, _prizeBaseScale, false, false);

            if (_collectButton != null)
            {
                _collectButton.interactable = false;
                SetRevealVisible(_collectButton.gameObject, _buttonBaseScale, false, false);
            }
        }

        public async UniTask PlayAsync()
        {
            AutoBindMissingReferences();
            CacheScales();
            var id = ++_sequenceId;

            await UniTask.Delay(System.TimeSpan.FromSeconds(_closedHoldSeconds));
            if (id != _sequenceId)
                return;

            OpenBox();

            await UniTask.Delay(System.TimeSpan.FromSeconds(_boxOpenPopSeconds + _prizeDelaySeconds));
            if (id != _sequenceId)
                return;

            ShowReveal(_prizeRoot, _prizeBaseScale, _prizePopSeconds);

            await UniTask.Delay(System.TimeSpan.FromSeconds(_prizePopSeconds + _buttonDelaySeconds));
            if (id != _sequenceId)
                return;

            if (_collectButton != null)
            {
                ShowReveal(_collectButton.gameObject, _buttonBaseScale, _buttonPopSeconds);
                _collectButton.interactable = true;
            }
        }

        private void OpenBox()
        {
            if (_boxImage == null)
                return;

            _boxImage.transform.DOKill();
            if (_openedSprite != null)
                _boxImage.sprite = _openedSprite;

            _boxImage.transform.localScale = ScaleBy(_boxBaseScale, 0.88f);
            _boxImage.transform.DOScale(_boxBaseScale, _boxOpenPopSeconds).SetEase(Ease.OutBack);
        }

        private void ShowReveal(GameObject target, Vector3 baseScale, float seconds)
        {
            if (target == null)
                return;

            target.transform.DOKill();
            var canvasGroup = EnsureCanvasGroup(target);
            canvasGroup.DOKill();

            target.SetActive(true);
            target.transform.localScale = ScaleBy(baseScale, 0.72f);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            target.transform.DOScale(baseScale, seconds).SetEase(Ease.OutBack);
            canvasGroup.DOFade(1f, seconds * 0.7f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private static void SetRevealVisible(GameObject target, Vector3 baseScale, bool visible, bool interactable)
        {
            if (target == null)
                return;

            target.transform.DOKill();
            target.transform.localScale = baseScale;
            var canvasGroup = EnsureCanvasGroup(target);
            canvasGroup.DOKill();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            target.SetActive(visible);
        }

        private void AutoBindMissingReferences()
        {
            if (_boxImage == null)
                _boxImage = GetFirstDirectChildImage();
            if (_collectButton == null)
                _collectButton = GetComponentInChildren<Button>(true);
            if (_prizeRoot == null)
                _prizeRoot = GetFirstDirectChildWithout<Image, Button>();
        }

        private Image GetFirstDirectChildImage()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var image = transform.GetChild(i).GetComponent<Image>();
                if (image != null)
                    return image;
            }

            return GetComponentInChildren<Image>(true);
        }

        private GameObject GetFirstDirectChildWithout<TA, TB>() where TA : Component where TB : Component
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<TA>() == null && child.GetComponent<TB>() == null)
                    return child.gameObject;
            }

            return null;
        }

        private void CacheScales()
        {
            if (_cachedScales)
                return;

            if (_boxImage != null)
                _boxBaseScale = _boxImage.transform.localScale;
            if (_prizeRoot != null)
                _prizeBaseScale = _prizeRoot.transform.localScale;
            if (_collectButton != null)
                _buttonBaseScale = _collectButton.transform.localScale;

            _cachedScales = true;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();

            return canvasGroup;
        }

        private static Vector3 ScaleBy(Vector3 baseScale, float multiplier)
        {
            return new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
        }
    }
}