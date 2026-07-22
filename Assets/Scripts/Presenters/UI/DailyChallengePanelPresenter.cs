using System;
using System.Globalization;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    public class DailyChallengePanelPresenter : MonoBehaviour
    {
        [Header("Calendar")]
        [SerializeField] private RectTransform _daysRoot;
        [SerializeField] private DailyChallengeDayPresenter _dayPrefab;
        [SerializeField] private TMP_Text _monthLabel;
        [SerializeField] private TMP_Text _selectedCountLabel;
        [SerializeField] private string _monthFormat = "MMMM";
        [SerializeField] private string _selectedMonthFormat = "MMMM,yyyy";
        [SerializeField] private bool _autoSelectToday = true;
        [SerializeField] private bool _rebuildWhenPanelOpens = true;

        [Header("Rules")]
        [SerializeField] private int _futurePlayableDays = 7;
        [SerializeField] private bool _futureDaysNeedAd = true;
        [SerializeField] private string _completedStorageKey = "DailyChallengeCompleted";
        [SerializeField] private int _seedSalt = 17;

        [Header("Play Button")]
        [SerializeField] private Button _playButton;
        [SerializeField] private bool _transparentPlayButtonBackground = true;
        [SerializeField] private GameObject _adIcon;
        [SerializeField] private Image _adIconImage;
        [SerializeField] private bool _logRewardedAdPlaceholder = true;

        [Header("Runtime Services")]
        [SerializeField] private bool _warnIfMissingReferences = true;

        private readonly CompositeDisposable _bindings = new();
        private readonly DailyChallengeCompletedData _completed = new();
        private Game _game;
        private IStorageService _storageService;
        private Action _closePanel;
        private DailyChallengeDayPresenter _selectedDay;
        private DateTime _visibleMonth;

        public void Initialize(Game game, IStorageService storageService, Action closePanel)
        {
            _game = game;
            _storageService = storageService;
            _closePanel = closePanel;
        }

        private void Start()
        {
            LoadCompletedDays();
            BuildMonth(DateTime.Today);

            ApplyTransparentPlayButtonBackground();

            if (_playButton != null)
                _playButton.OnClickAsObservable().Subscribe(_ => PlaySelectedDay()).AddTo(_bindings);

            _game?.DailyGameWon.Subscribe(MarkCompleted).AddTo(_bindings);
        }

        private void OnEnable()
        {
            if (!_rebuildWhenPanelOpens)
            {
                RefreshSelection();
                return;
            }

            var today = DateTime.Today;
            if (_visibleMonth.Year != today.Year || _visibleMonth.Month != today.Month)
                BuildMonth(today);
            else
                RefreshMonthState();
        }

        private void OnDestroy()
        {
            _bindings.Dispose();
        }

        public void BuildMonth(DateTime date)
        {
            if (!HasRequiredReferences())
                return;

            _visibleMonth = new DateTime(date.Year, date.Month, 1);
            if (_monthLabel != null)
                _monthLabel.text = _visibleMonth.ToString(_monthFormat, CultureInfo.InvariantCulture);

            ClearGeneratedDays();
            _dayPrefab.gameObject.SetActive(false);
            _selectedDay = null;

            var leadingBlanks = GetMondayBasedDayIndex(_visibleMonth.DayOfWeek);
            for (var i = 0; i < leadingBlanks; i++)
            {
                var blank = Instantiate(_dayPrefab, _daysRoot);
                blank.name = "Empty Day";
                blank.gameObject.SetActive(true);
                blank.SetupBlank();
            }

            var daysInMonth = DateTime.DaysInMonth(_visibleMonth.Year, _visibleMonth.Month);
            for (var day = 1; day <= daysInMonth; day++)
            {
                var cell = Instantiate(_dayPrefab, _daysRoot);
                cell.name = $"Day {day}";
                cell.gameObject.SetActive(true);
                cell.Setup(new DateTime(_visibleMonth.Year, _visibleMonth.Month, day), this);
            }

            RefreshSelection();
        }

        public void Select(DailyChallengeDayPresenter day)
        {
            if (day == null || !day.IsSelectable)
                return;

            if (_selectedDay != null && _selectedDay != day)
                _selectedDay.SetSelected(false);

            _selectedDay = day;
            _selectedDay.SetSelected(true);
            RefreshSelection();
        }

        public bool IsCompleted(DateTime date)
        {
            return _completed.Days.Contains(ToKey(date));
        }

        public bool IsSelectableDate(DateTime date)
        {
            if (IsCompleted(date))
                return false;

            var delta = (date.Date - DateTime.Today).Days;
            return delta <= Mathf.Max(0, _futurePlayableDays);
        }

        public bool NeedsAd(DateTime date)
        {
            if (!_futureDaysNeedAd)
                return false;

            var delta = (date.Date - DateTime.Today).Days;
            return delta > 0 && delta <= Mathf.Max(0, _futurePlayableDays);
        }

        private void RefreshMonthState()
        {
            if (!HasRequiredReferences())
                return;

            for (var i = 0; i < _daysRoot.childCount; i++)
            {
                var day = _daysRoot.GetChild(i).GetComponent<DailyChallengeDayPresenter>();
                if (day != null && day.HasDate)
                    day.Setup(day.Date, this);
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selectedDay == null || !_selectedDay.IsSelectable)
            {
                _selectedDay = FindDefaultSelection();
                if (_selectedDay != null)
                    _selectedDay.SetSelected(true);
            }

            var hasSelection = _selectedDay != null;
            if (_selectedCountLabel != null)
                _selectedCountLabel.text = _visibleMonth.ToString(_selectedMonthFormat, CultureInfo.InvariantCulture);

            if (_playButton != null)
                _playButton.interactable = hasSelection;
            ApplyTransparentPlayButtonBackground();
            if (_adIconImage == null && _adIcon != null)
                _adIconImage = _adIcon.GetComponent<Image>();
            if (_adIconImage != null)
                _adIconImage.enabled = hasSelection && NeedsAd(_selectedDay.Date);
        }

        private void ApplyTransparentPlayButtonBackground()
        {
            if (!_transparentPlayButtonBackground || _playButton == null)
                return;

            var colors = _playButton.colors;
            colors.normalColor = WithAlpha(colors.normalColor, 0f);
            colors.highlightedColor = WithAlpha(colors.highlightedColor, 0f);
            colors.pressedColor = WithAlpha(colors.pressedColor, 0f);
            colors.selectedColor = WithAlpha(colors.selectedColor, 0f);
            colors.disabledColor = WithAlpha(colors.disabledColor, 0f);
            _playButton.colors = colors;

            var graphic = _playButton.targetGraphic != null
                ? _playButton.targetGraphic
                : _playButton.GetComponent<Graphic>();
            if (graphic == null)
                return;

            var color = graphic.color;
            color.a = 0f;
            graphic.color = color;
            graphic.raycastTarget = true;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private DailyChallengeDayPresenter FindDefaultSelection()
        {
            if (_daysRoot == null)
                return null;

            var today = DateTime.Today;
            DailyChallengeDayPresenter fallback = null;
            for (var i = 0; i < _daysRoot.childCount; i++)
            {
                var day = _daysRoot.GetChild(i).GetComponent<DailyChallengeDayPresenter>();
                if (day == null || !day.IsSelectable)
                    continue;
                if (_autoSelectToday && day.Date.Date == today)
                    return day;
                fallback ??= day;
            }

            return fallback;
        }

        private void PlaySelectedDay()
        {
            if (_selectedDay == null || !_selectedDay.IsSelectable || _game == null)
                return;

            if (NeedsAd(_selectedDay.Date) && _logRewardedAdPlaceholder)
                Debug.Log("TODO: show rewarded ad before starting future daily challenge.");

            _game.QueueDailyMatch(ToKey(_selectedDay.Date), SeedForDate(_selectedDay.Date));
            _closePanel?.Invoke();
            _game.NewMatchCommand.Execute();
        }

        private void MarkCompleted(string key)
        {
            if (string.IsNullOrEmpty(key) || _completed.Days.Contains(key))
                return;

            _completed.Days.Add(key);
            _storageService?.Save(_completedStorageKey, _completed);
            BuildMonth(DateTime.Today);
        }

        private void LoadCompletedDays()
        {
            if (_storageService == null)
                return;

            var saved = _storageService.Load<DailyChallengeCompletedData>(_completedStorageKey);
            if (saved == null)
                return;

            _completed.Days.Clear();
            _completed.Days.AddRange(saved.Days);
        }

        private void ClearGeneratedDays()
        {
            for (var i = _daysRoot.childCount - 1; i >= 0; i--)
            {
                var child = _daysRoot.GetChild(i).gameObject;
                if (child != _dayPrefab.gameObject)
                    Destroy(child);
            }
        }

        private bool HasRequiredReferences()
        {
            var ok = _daysRoot != null && _dayPrefab != null;
            if (!ok && _warnIfMissingReferences)
                Debug.LogWarning("DailyChallengePanelPresenter needs Days Root and Day Prefab assigned in the inspector.", this);
            return ok;
        }

        private static int GetMondayBasedDayIndex(DayOfWeek dayOfWeek)
        {
            return ((int)dayOfWeek + 6) % 7;
        }

        private int SeedForDate(DateTime date)
        {
            unchecked
            {
                var seed = _seedSalt;
                seed = seed * 31 + date.Year;
                seed = seed * 31 + date.Month;
                seed = seed * 31 + date.Day;
                return seed;
            }
        }

        private static string ToKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        [Serializable]
        private class DailyChallengeCompletedData
        {
            public System.Collections.Generic.List<string> Days = new();
        }
    }
}
