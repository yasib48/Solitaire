using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Keeps the play-menu level bar in sync and plays the end-of-game XP reveal.
    ///     Slide-in/out visibility is delegated to <see cref="UiSlidePanel"/>; this
    ///     class only owns the level/XP display and the level-up reward reveal steps.
    /// </summary>
    [RequireComponent(typeof(UiSlidePanel))]
    public class LevelBarPresenter : MonoBehaviour
    {
        private const float StepDuration = 0.2f;
        private const float FillDuration = 0.7f;
        private const int StepDelayMs = 180;
        private const int HoldMs = 520;
        private const float MessageSlideDistance = 280f;
        private const float MessageSlideDuration = 0.26f;

        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _progressLabel;
        [SerializeField] private Transform _starIcon;

        [Inject] private readonly ILevelService _levelService;

        private UiSlidePanel _slidePanel;
        private int _displayedLevel;
        private int _displayedExp;
        private int _displayedRequired;
        private bool _displayedInitialized;
        // The reveal is two sibling nodes under the level bar: "Text" holds the
        // message ("Tebrikler Kazandınız") and "Star" holds the number ("+15").
        // Both slide in/out together and update their text per XP segment.
        private CanvasGroup _messageGroup;
        private CanvasGroup _starGroup;
        private Transform _messageRoot;
        private Transform _starRoot;
        private RectTransform _messageRect;
        private RectTransform _starRect;
        private Vector2 _messageBasePos;
        private Vector2 _starBasePos;
        private bool _baseCached;
        private TMP_Text _messageLabel;
        private TMP_Text _earnedLabel;

        /// <summary>True while the bar is open or mid-open-transition.</summary>
        public bool IsVisible => SlidePanel.IsOpen;

        // Lazily fetched instead of cached in Awake: this GameObject starts
        // inactive in the scene, and Unity never calls Awake on a component
        // until its GameObject is activated for the first time. Anything
        // called before that first activation (e.g. the very first
        // SlideInAsync request) would otherwise see a null field.
        private UiSlidePanel SlidePanel
        {
            get
            {
                if (_slidePanel == null)
                    _slidePanel = GetComponent<UiSlidePanel>();
                return _slidePanel;
            }
        }

        private void Start()
        {
            EnsureDisplayedInitialized();
        }

        private void EnsureDisplayedInitialized()
        {
            if (_displayedInitialized)
                return;

            _displayedInitialized = true;
            _displayedLevel = _levelService.Level.Value;
            _displayedExp = _levelService.Experience.Value;
            _displayedRequired = _levelService.RequiredExperience.Value;
            SetImmediate(_displayedLevel, _displayedExp, _displayedRequired);
            CacheRewardParts();
            ResetRewardParts();
        }

        public UniTask SlideInAsync()
        {
            EnsureDisplayedInitialized();
            return SlidePanel.OpenAsync();
        }

        public UniTask SlideOutAsync()
        {
            return SlidePanel.CloseAsync();
        }

        public async UniTask PlayGainAsync(Game.GameEndResult result, System.Action<int> onSegmentFill = null)
        {
            EnsureDisplayedInitialized();
            CacheRewardParts();

            SetImmediate(_displayedLevel, _displayedExp, _displayedRequired);
            ResetRewardParts();

            await SlidePanel.OpenAsync();
            await UniTask.Delay(StepDelayMs);

            // Reveal the XP one source at a time: base win, then combo, then
            // best-score. Each message slides in from the side, fills its share
            // of the bar, then slides off before the next one arrives.
            var segments = BuildSegments(result);

            if (_messageRoot != null)
                _messageRoot.gameObject.SetActive(true);
            if (_starRoot != null)
                _starRoot.gameObject.SetActive(true);

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (_messageLabel != null) _messageLabel.text = segment.label;
                if (_earnedLabel != null) _earnedLabel.text = segment.number;

                await SlideMessageInAsync();
                // Fly the stars up and pop the number just as the bar starts
                // growing by this chunk, so they arrive as it fills.
                if (segment.xp > 0)
                    onSegmentFill?.Invoke(segment.xp);
                PunchAsync(_starRoot).Forget();
                await FillBySegmentAsync(segment.xp);
                await UniTask.Delay(StepDelayMs);
                await SlideMessageOutAsync();
            }

            await UniTask.Delay(HoldMs / 2);
            await SlidePanel.CloseAsync();

            // Settle to the service's authoritative final state.
            _displayedLevel = _levelService.Level.Value;
            _displayedExp = _levelService.Experience.Value;
            _displayedRequired = _levelService.RequiredExperience.Value;
            SetImmediate(_displayedLevel, _displayedExp, _displayedRequired);
            ResetRewardParts();
        }

        // Each segment carries the top message, the number shown beneath it, and
        // how much XP it fills. The number always equals the XP it adds ("+10"),
        // so the bar visibly grows by exactly the shown amount for every step;
        // the combo streak count lives in the message ("Combo x3").
        private static List<(string label, string number, int xp)> BuildSegments(Game.GameEndResult result)
        {
            var segments = new List<(string label, string number, int xp)>();
            if (result == null)
            {
                segments.Add(("Tebrikler kazandınız", "+0", 0));
                return segments;
            }

            var baseXp = Mathf.Max(0, result.BaseXp);
            segments.Add(("Tebrikler kazandınız", $"+{baseXp}", baseXp));
            if (result.ComboXp > 0)
                segments.Add(($"Combo x{result.ComboCount}", $"+{result.ComboXp}", result.ComboXp));
            if (result.BestScoreXp > 0)
                segments.Add(("Yeni rekor!", $"+{result.BestScoreXp}", result.BestScoreXp));

            return segments;
        }

        // Advances the displayed XP by the chunk, animating the bar and rolling
        // the level over (with a star punch) whenever it fills.
        private async UniTask FillBySegmentAsync(int xp)
        {
            var remaining = xp;
            var safety = 0;
            while (remaining > 0 && safety++ < 50)
            {
                var space = _displayedRequired - _displayedExp;
                if (space <= 0)
                {
                    await RollLevelAsync();
                    continue;
                }

                var add = Mathf.Min(space, remaining);
                await FillAsync(_displayedExp, _displayedExp + add, _displayedRequired, _displayedLevel);
                _displayedExp += add;
                remaining -= add;

                if (_displayedExp >= _displayedRequired)
                    await RollLevelAsync();
            }
        }

        private async UniTask RollLevelAsync()
        {
            await PunchAsync(_starIcon);
            _displayedLevel += 1;
            _displayedExp = 0;
            _displayedRequired = _levelService.GetRequiredExperienceForLevel(_displayedLevel);
            SetImmediate(_displayedLevel, 0, _displayedRequired);
            await UniTask.Delay(StepDelayMs);
        }

        // Slides both the message ("Text") and the number ("Star") in together
        // from the right; the message tween drives completion.
        private async UniTask SlideMessageInAsync()
        {
            var done = false;
            SlideNodeIn(_messageRect, _messageGroup, _messageBasePos, () => done = true);
            SlideNodeIn(_starRect, _starGroup, _starBasePos, null);
            if (_messageRect == null)
                done = true;
            await UniTask.WaitUntil(() => done);
        }

        private async UniTask SlideMessageOutAsync()
        {
            var done = false;
            SlideNodeOut(_messageRect, _messageGroup, _messageBasePos, () => done = true);
            SlideNodeOut(_starRect, _starGroup, _starBasePos, null);
            if (_messageRect == null)
                done = true;
            await UniTask.WaitUntil(() => done);
        }

        private static void SlideNodeIn(RectTransform rect, CanvasGroup group, Vector2 basePos, System.Action onDone)
        {
            if (rect == null)
            {
                onDone?.Invoke();
                return;
            }

            rect.DOKill();
            group?.DOKill();

            rect.anchoredPosition = basePos + new Vector2(MessageSlideDistance, 0f);
            if (group != null)
                group.alpha = 0f;

            rect.DOAnchorPos(basePos, MessageSlideDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            if (group != null)
                group.DOFade(1f, MessageSlideDuration * 0.8f).SetUpdate(true).OnComplete(() => onDone?.Invoke());
            else
                DOVirtual.DelayedCall(MessageSlideDuration, () => onDone?.Invoke()).SetUpdate(true);
        }

        private static void SlideNodeOut(RectTransform rect, CanvasGroup group, Vector2 basePos, System.Action onDone)
        {
            if (rect == null)
            {
                onDone?.Invoke();
                return;
            }

            rect.DOKill();
            group?.DOKill();

            rect.DOAnchorPos(basePos - new Vector2(MessageSlideDistance, 0f), MessageSlideDuration)
                .SetEase(Ease.InCubic).SetUpdate(true);
            if (group != null)
                group.DOFade(0f, MessageSlideDuration * 0.8f).SetUpdate(true)
                    .OnComplete(() => { rect.anchoredPosition = basePos; onDone?.Invoke(); });
            else
                DOVirtual.DelayedCall(MessageSlideDuration, () => { rect.anchoredPosition = basePos; onDone?.Invoke(); }).SetUpdate(true);
        }

        private void SetImmediate(int level, int exp, int required)
        {
            if (_levelLabel != null) _levelLabel.text = $"Sv. {level}";
            if (_progressLabel != null) _progressLabel.text = $"{exp}/{required}";
            if (_progressSlider != null) _progressSlider.value = required > 0 ? (float)exp / required : 0f;
        }

        private async UniTask FillAsync(int fromExp, int toExp, int required, int level)
        {
            if (_progressSlider == null || required <= 0)
                return;

            var done = false;
            var value = (float)fromExp;

            DOTween
                .To(() => value, x => value = x, toExp, FillDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnUpdate(() =>
                {
                    var rounded = Mathf.RoundToInt(value);
                    if (_levelLabel != null) _levelLabel.text = $"Sv. {level}";
                    if (_progressLabel != null) _progressLabel.text = $"{rounded}/{required}";
                    _progressSlider.value = Mathf.Clamp01(value / required);
                })
                .OnComplete(() => done = true);

            await UniTask.WaitUntil(() => done);
        }

        private static UniTask PunchAsync(Transform target)
        {
            if (target == null)
                return UniTask.CompletedTask;

            var done = false;
            target.DOKill();
            target.DOPunchScale(Vector3.one * 0.22f, 0.22f, 8, 0.8f).SetUpdate(true).OnComplete(() => done = true);
            return UniTask.WaitUntil(() => done);
        }

        private void CacheRewardParts()
        {
            if (_messageRoot == null)
                _messageRoot = transform.Find("Text");
            if (_starRoot == null)
                _starRoot = transform.Find("Star");

            if (_messageGroup == null && _messageRoot != null)
                _messageGroup = EnsureCanvasGroup(_messageRoot.gameObject);
            if (_starGroup == null && _starRoot != null)
                _starGroup = EnsureCanvasGroup(_starRoot.gameObject);

            if (_messageLabel == null && _messageRoot != null)
                _messageLabel = _messageRoot.GetComponentInChildren<TMP_Text>(true);
            if (_earnedLabel == null && _starRoot != null)
                _earnedLabel = _starRoot.GetComponentInChildren<TMP_Text>(true);

            if (_messageRect == null && _messageRoot != null)
                _messageRect = _messageRoot as RectTransform;
            if (_starRect == null && _starRoot != null)
                _starRect = _starRoot as RectTransform;

            if (!_baseCached && (_messageRect != null || _starRect != null))
            {
                if (_messageRect != null) _messageBasePos = _messageRect.anchoredPosition;
                if (_starRect != null) _starBasePos = _starRect.anchoredPosition;
                _baseCached = true;
            }
        }

        private void ResetRewardParts()
        {
            ResetRevealNode(_messageRoot, _messageRect, _messageGroup, _messageBasePos);
            ResetRevealNode(_starRoot, _starRect, _starGroup, _starBasePos);
        }

        private void ResetRevealNode(Transform root, RectTransform rect, CanvasGroup group, Vector2 basePos)
        {
            if (root != null)
            {
                root.DOKill();
                root.localScale = Vector3.one;
                root.gameObject.SetActive(false);
            }

            if (rect != null && _baseCached)
                rect.anchoredPosition = basePos;

            if (group != null)
                group.alpha = 0f;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
                group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }
    }
}
