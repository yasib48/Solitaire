using DG.Tweening;
using Solitaire.Models;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    public class GameControlsPresenter : MonoBehaviour
    {
        [SerializeField] private Button _buttonUndo;
        [SerializeField] private Button _buttonHint;
        [SerializeField] private Button _buttonMagicWand;
        [SerializeField] private Button _buttonComplete;

        [Inject] private readonly GameControls _gameControls;

        private RectTransform _completeRect;
        private CanvasGroup _completeGroup;
        private Vector3 _completeBaseScale = Vector3.one;
        private bool _completeShown;

        private void Start()
        {
            if (_buttonUndo != null)
                _gameControls.UndoCommand.BindTo(_buttonUndo).AddTo(this);
            if (_buttonHint != null)
                _gameControls.HintCommand.BindTo(_buttonHint).AddTo(this);
            if (_buttonMagicWand != null)
                _gameControls.MagicWandCommand.BindTo(_buttonMagicWand).AddTo(this);

            if (_buttonComplete != null)
            {
                CacheCompleteButton();

                // BindTo drives interactable from the command's canExecute; we
                // additionally play an animated show/hide on the same signal so
                // the button pops in rather than snapping on.
                _gameControls.AutoCompleteCommand.BindTo(_buttonComplete).AddTo(this);
                _gameControls.AutoCompleteCommand.CanExecute
                    .Subscribe(SetCompleteVisible)
                    .AddTo(this);
            }
        }

        private void CacheCompleteButton()
        {
            _completeRect = _buttonComplete.transform as RectTransform;
            _completeBaseScale = _completeRect != null && _completeRect.localScale != Vector3.zero
                ? _completeRect.localScale
                : Vector3.one;

            _completeGroup = _buttonComplete.GetComponent<CanvasGroup>();
            if (_completeGroup == null)
                _completeGroup = _buttonComplete.gameObject.AddComponent<CanvasGroup>();

            // Start hidden; the CanExecute subscription fires once on subscribe
            // and will show it if it's already available.
            _buttonComplete.gameObject.SetActive(false);
            _completeShown = false;
        }

        private void SetCompleteVisible(bool visible)
        {
            if (_buttonComplete == null || _completeRect == null)
                return;

            if (visible == _completeShown)
                return;

            _completeShown = visible;
            var go = _buttonComplete.gameObject;

            _completeRect.DOKill();
            _completeGroup.DOKill();

            if (visible)
            {
                go.SetActive(true);
                _completeRect.localScale = _completeBaseScale * 0.5f;
                _completeGroup.alpha = 0f;

                // Pop in with a springy overshoot + fade, then a gentle looping
                // pulse so it keeps drawing the eye while it's available.
                _completeGroup.DOFade(1f, 0.18f).SetUpdate(true);
                _completeRect.DOScale(_completeBaseScale, 0.42f)
                    .SetEase(Ease.OutBack, 2.2f)
                    .SetUpdate(true)
                    .OnComplete(StartCompletePulse);
            }
            else
            {
                _completeGroup.DOFade(0f, 0.14f).SetUpdate(true);
                _completeRect.DOScale(_completeBaseScale * 0.5f, 0.14f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _completeRect.localScale = _completeBaseScale;
                        go.SetActive(false);
                    });
            }
        }

        private void StartCompletePulse()
        {
            if (!_completeShown || _completeRect == null)
                return;

            _completeRect.localScale = _completeBaseScale;
            _completeRect.DOScale(_completeBaseScale * 1.06f, 0.7f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }
}
