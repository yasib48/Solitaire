using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    public class GameInfoPresenter : OrientationAwarePresenter
    {
        [SerializeField] private TextMeshProUGUI _labelPoints;
        [SerializeField] private TextMeshProUGUI _labelTime;
        [SerializeField] private TextMeshProUGUI _labelMoves;
        [Inject] private readonly IMovesService _movesService;

        [Inject] private readonly IPointsService _pointsService;

        [Inject] private readonly ITimerService _timerService;

        [Inject] private readonly GameState _gameState;

        // Elapsed time of the current game, shown as "m:ss" (e.g. 0:00, 1:05).
        // Counts only while playing and resets on a new deal. Backed by the
        // shared ITimerService so the results table can read the finish time.
        private string _lastTime;

        // The value currently shown on the points label. Points tick up to their
        // new total (classic count-up) instead of snapping in one jump.
        private const float PointsCountDuration = 0.4f;
        private int _displayedPoints;
        private Tweener _pointsTween;

        protected override void Start()
        {
            base.Start();

            _pointsService.Points.Subscribe(AnimatePoints).AddTo(this);
            _pointsService.OnScored.Subscribe(e => ShowScorePopups(e.delta, e.worldPos)).AddTo(this);
            _movesService.Moves.Subscribe(v => _labelMoves.text = v.ToString()).AddTo(this);

            SetTimeText();
        }

        private void Update()
        {
            switch (_gameState.State.Value)
            {
                case Game.State.Dealing:
                    _timerService.Reset();
                    break;
                case Game.State.Playing:
                    _timerService.Tick(Time.deltaTime);
                    break;
            }

            SetTimeText();
        }

        private void AnimatePoints(int target)
        {
            _pointsTween?.Kill();

            // Only count up on gains; snap instantly on a decrease or reset so we
            // don't play a backwards countdown when a new game starts.
            if (target <= _displayedPoints)
            {
                _displayedPoints = target;
                _labelPoints.text = target.ToString();
                return;
            }

            _pointsTween = DOTween
                .To(
                    () => _displayedPoints,
                    x =>
                    {
                        _displayedPoints = x;
                        _labelPoints.text = x.ToString();
                    },
                    target,
                    PointsCountDuration
                )
                .SetEase(Ease.OutQuad);
        }

        private void SetTimeText()
        {
            if (_labelTime == null)
                return;

            var minutes = (int)(_timerService.Elapsed / 60f);
            var seconds = (int)(_timerService.Elapsed % 60f);
            var text = $"{minutes}:{seconds:00}";
            if (text == _lastTime)
                return;

            _lastTime = text;
            _labelTime.text = text;
        }

        private void ShowScorePopups(int delta, Vector3 worldPos)
        {
            ShowHudPopup(delta);
            ShowWorldPopup(delta, worldPos);
        }

        private void ShowHudPopup(int delta)
        {
            var go = new GameObject("ScorePopup");
            go.transform.SetParent(_labelPoints.transform.parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = $"+{delta}";
            tmp.fontSize = _labelPoints.fontSize * 0.85f;
            tmp.font = _labelPoints.font;
            tmp.fontMaterial = _labelPoints.fontMaterial;
            tmp.color = new Color(0.2f, 0.9f, 0.3f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = _labelPoints.rectTransform.anchoredPosition + new Vector2(0, -40f);
            rt.sizeDelta = _labelPoints.rectTransform.sizeDelta;

            var seq = DOTween.Sequence();
            seq.Append(rt.DOAnchorPosY(rt.anchoredPosition.y - 60f, 0.8f).SetEase(Ease.OutQuad));
            seq.Join(tmp.DOFade(0f, 0.8f).SetDelay(0.3f));
            seq.OnComplete(() => Destroy(go));
        }

        private void ShowWorldPopup(int delta, Vector3 worldPos)
        {
            var go = new GameObject("WorldScorePopup");
            go.transform.position = worldPos + Vector3.back * 0.5f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = $"+{delta}";
            tmp.fontSize = 5f;
            tmp.font = _labelPoints.font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 0.3f);
            tmp.sortingOrder = 999;

            var seq = DOTween.Sequence();
            seq.Append(go.transform.DOMoveY(worldPos.y + 1f, 0.8f).SetEase(Ease.OutQuad));
            seq.Join(tmp.DOFade(0f, 0.8f).SetDelay(0.3f));
            seq.OnComplete(() => Destroy(go));
        }

        protected override void OnOrientationChanged(bool isLandscape) { }
    }
}