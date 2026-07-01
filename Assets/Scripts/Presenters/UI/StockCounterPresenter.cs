using Solitaire.Models;
using TMPro;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    // Shows how many cards are left in the stock pile. The number follows every
    // draw and refill, and the whole group (text + background) is hidden when the
    // stock is empty.
    //
    // Setup: put this on an always-active object (e.g. the Stock pile object).
    // Assign _root to the text+background group to toggle, and _label to the text.
    public class StockCounterPresenter : MonoBehaviour
    {
        [Tooltip("Text + background group; hidden when the stock is empty.")]
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private TMP_Text _label;

        [Inject]
        private readonly Game _game;

        private int _lastCount = -1;

        private void Update()
        {
            var count = _game.PileStock != null ? _game.PileStock.Cards.Count : 0;

            if (count == _lastCount)
                return;

            _lastCount = count;

            if (_label != null)
                _label.text = count.ToString();

            if (_root != null)
                _root.SetActive(count > 0);
        }
    }
}
