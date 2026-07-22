using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    public class DailyChallengeDayPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _buttonImage;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private GameObject _selected;
        [SerializeField] private GameObject _winningIcon;
        [SerializeField] private GameObject _futureMarker;

        [Header("Button Colors")]
        [SerializeField] private Color _buttonColor = Color.white;

        [Header("Text Colors")]
        [SerializeField] private Color _normalTextColor = Color.black;
        [SerializeField] private Color _lockedTextColor = new(0.58f, 0.58f, 0.58f, 1f);
        [SerializeField] private Color _completedTextColor = new(0.24f, 0.6f, 0.36f, 1f);

        [Header("State")]
        [SerializeField] private bool _hideCountWhenBlank = true;

        private DailyChallengePanelPresenter _owner;

        public DateTime Date { get; private set; }
        public bool HasDate { get; private set; }
        public bool IsSelectable { get; private set; }

        public void Setup(DateTime date, DailyChallengePanelPresenter owner)
        {
            Date = date.Date;
            HasDate = true;
            _owner = owner;

            var completed = owner != null && owner.IsCompleted(Date);
            IsSelectable = owner != null && owner.IsSelectableDate(Date);

            if (_countLabel != null)
            {
                _countLabel.gameObject.SetActive(true);
                _countLabel.text = Date.Day.ToString();
                _countLabel.color = completed ? _completedTextColor : IsSelectable ? _normalTextColor : _lockedTextColor;
            }

            SetSelected(false);
            SetActive(_winningIcon, completed);
            SetButtonVisuals();
            SetActive(_futureMarker, !completed && owner != null && owner.NeedsAd(Date));
            BindButton(IsSelectable);
        }

        public void SetupBlank()
        {
            HasDate = false;
            IsSelectable = false;
            _owner = null;
            Date = default;

            SetSelected(false);
            SetActive(_winningIcon, false);
            SetActive(_futureMarker, false);

            if (_countLabel != null)
            {
                _countLabel.text = string.Empty;
                _countLabel.gameObject.SetActive(!_hideCountWhenBlank);
            }

            SetButtonVisuals();

            BindButton(false);
        }

        public void SetSelected(bool selected)
        {
            SetActive(_selected, selected);
        }

        private void BindButton(bool interactable)
        {
            if (_button == null)
                return;

            _button.interactable = interactable;
            _button.onClick.RemoveAllListeners();
            if (interactable)
                _button.OnClickAsObservable().Subscribe(_ => _owner?.Select(this)).AddTo(this);
        }

        private void SetButtonVisuals()
        {
            if (_buttonImage == null && _button != null)
                _buttonImage = _button.targetGraphic as Image;
            if (_buttonImage != null)
                _buttonImage.color = _buttonColor;

            if (_button == null)
                return;

            var colors = _button.colors;
            colors.normalColor = _buttonColor;
            colors.highlightedColor = _buttonColor;
            colors.pressedColor = _buttonColor;
            colors.selectedColor = _buttonColor;
            colors.disabledColor = _buttonColor;
            _button.colors = colors;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
