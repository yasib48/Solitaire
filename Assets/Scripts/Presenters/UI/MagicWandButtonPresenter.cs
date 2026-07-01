using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Toggles the magic wand button between its "charges available" look
    ///     (number badge showing the remaining count) and its "get more" look
    ///     (plus icon) based on how many charges the player currently owns.
    ///     When the count reaches zero the plus icon is shown instead of a "0",
    ///     which is where the (future) rewarded-ad top-up flow hooks in.
    /// </summary>
    public class MagicWandButtonPresenter : MonoBehaviour
    {
        [Header("Charges available")]
        [SerializeField] private GameObject _numberIcon;

        [SerializeField] private TMP_Text _countText;

        [Header("Out of charges")]
        [SerializeField] private GameObject _plusIcon;

        [Inject] private readonly IMagicWandService _magicWandService;

        private void Start()
        {
            _magicWandService.Count.Subscribe(UpdateDisplay).AddTo(this);
        }

        private void UpdateDisplay(int count)
        {
            var hasCharges = count > 0;

            if (_numberIcon != null)
                _numberIcon.SetActive(hasCharges);
            if (_plusIcon != null)
                _plusIcon.SetActive(!hasCharges);
            if (_countText != null && hasCharges)
                _countText.text = count.ToString();
        }
    }
}
