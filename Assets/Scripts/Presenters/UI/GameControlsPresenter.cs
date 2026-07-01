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

        [Inject] private readonly GameControls _gameControls;

        private void Start()
        {
            if (_buttonUndo != null)
                _gameControls.UndoCommand.BindTo(_buttonUndo).AddTo(this);
            if (_buttonHint != null)
                _gameControls.HintCommand.BindTo(_buttonHint).AddTo(this);
            if (_buttonMagicWand != null)
                _gameControls.MagicWandCommand.BindTo(_buttonMagicWand).AddTo(this);
        }
    }
}