using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Drives the level-up reward strip: a triangle marker slides back and forth
    ///     across the multiplier track (x2 x3 x4 x5 x4 x3 x2) and the "get" button's
    ///     multiplier label updates live to whatever segment the marker is currently
    ///     over. When the reward is claimed the marker freezes and
    ///     <see cref="CurrentMultiplier" /> is the amount that was won.
    ///
    ///     Put this on the Prize Image panel. It runs on unscaled time so it still
    ///     animates while the game is paused behind the popup.
    /// </summary>
    public class PrizeMarkerPresenter : MonoBehaviour
    {
        [Tooltip("The triangle that slides across the reward strip.")]
        [SerializeField] private RectTransform _marker;

        [Tooltip("The 'x2' text on the reward (with-ad) button; updated live.")]
        [SerializeField] private TMP_Text _multiplierLabel;

        [Tooltip("How far the marker travels to each side of centre, in the marker's anchored units.")]
        [SerializeField] private float _travel = 130f;

        [Tooltip("Seconds for one full left-to-right sweep.")]
        [SerializeField] private float _sweepDuration = 1.4f;

        [Tooltip("Reward multiplier for each segment, left to right.")]
        [SerializeField] private int[] _multipliers = { 2, 3, 4, 5, 4, 3, 2 };

        [Tooltip("Optional prefix for the label, e.g. \"x\" -> \"x3\".")]
        [SerializeField] private string _labelPrefix = "x";

        private Tweener _tween;
        private float _markerBaseY;
        private bool _initialised;

        /// <summary>The multiplier the marker is currently pointing at.</summary>
        public int CurrentMultiplier { get; private set; }

        private void OnEnable()
        {
            Restart();
        }

        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
        }

        /// <summary>Start (or restart) the marker sweeping across the track.</summary>
        public void Restart()
        {
            if (_marker == null || _multipliers == null || _multipliers.Length == 0)
                return;

            if (!_initialised)
            {
                _markerBaseY = _marker.anchoredPosition.y;
                _initialised = true;
            }

            _tween?.Kill();

            var t = 0f;
            Apply(0f);
            _tween = DOTween
                .To(() => t, x => { t = x; Apply(x); }, 1f, _sweepDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        /// <summary>Stop the marker where it is; <see cref="CurrentMultiplier" /> holds the result.</summary>
        public void Freeze()
        {
            _tween?.Kill();
            _tween = null;
        }

        // t in [0,1] across the whole strip, left to right.
        private void Apply(float t)
        {
            var x = Mathf.Lerp(-_travel, _travel, t);
            _marker.anchoredPosition = new Vector2(x, _markerBaseY);

            var index = Mathf.Clamp(Mathf.FloorToInt(t * _multipliers.Length), 0, _multipliers.Length - 1);
            CurrentMultiplier = _multipliers[index];

            if (_multiplierLabel != null)
                _multiplierLabel.text = $"{_labelPrefix}{CurrentMultiplier}";
        }
    }
}
