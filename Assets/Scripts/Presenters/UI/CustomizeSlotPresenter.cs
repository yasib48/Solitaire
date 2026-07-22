using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     One background slot in the Customize screen (goes on the "Slot B" prefab).
    ///     Purely inspector-driven: assign the background this slot represents, its
    ///     price and how it unlocks, plus the child objects to toggle. All the
    ///     actual logic (spending, ad, applying/persisting the background) lives in
    ///     <see cref="CustomizeController"/>; this class just holds config, shows
    ///     its own visual state, and reports clicks.
    /// </summary>
    public class CustomizeSlotPresenter : MonoBehaviour
    {
        public enum UnlockKind
        {
            Free, // owned from the start, just selectable
            Paid, // buy with coins
            Ad    // unlock by watching an ad
        }

        [Header("Config (set per slot)")]
        [Tooltip("Unique id used to remember whether this background is owned/selected.")]
        [SerializeField] private string _backgroundId;

        [Tooltip("The background this slot represents: shown in Item and applied to the game when selected.")]
        [SerializeField] private Sprite _backgroundSprite;

        [Tooltip("Free = already owned; Paid = buy with coins; Ad = watch an ad.")]
        [SerializeField] private UnlockKind _unlock = UnlockKind.Free;

        [Tooltip("Coin cost when Unlock = Paid.")]
        [SerializeField] private int _price;

        [Header("Slot children (wire in inspector)")]
        [Tooltip("The button on this slot (defaults to a Button on this object).")]
        [SerializeField] private Button _button;

        [Tooltip("The 'Item' image the background preview sprite is put on.")]
        [SerializeField] private Image _itemImage;

        [Tooltip("The 'Lock' object, shown while this slot is still locked.")]
        [SerializeField] private GameObject _lockObject;

        [Tooltip("The 'Coin' object, shown when this slot is locked and paid-for.")]
        [SerializeField] private GameObject _coinObject;

        [Tooltip("The 'Add' object, shown when this slot is locked and ad-unlocked.")]
        [SerializeField] private GameObject _addObject;

        [Tooltip("The 'Check' object, shown when this slot is the selected background.")]
        [SerializeField] private GameObject _checkObject;

        [Tooltip("Price label on the slot (shows the coin cost while locked & paid).")]
        [SerializeField] private TMP_Text _priceLabel;

        private CustomizeController _controller;

        public string BackgroundId => _backgroundId;
        public Sprite BackgroundSprite => _backgroundSprite;
        public UnlockKind Unlock => _unlock;
        public int Price => _price;

        public void Initialize(CustomizeController controller)
        {
            _controller = controller;

            if (_button == null)
                _button = GetComponent<Button>();

            // Static config visuals.
            if (_itemImage != null && _backgroundSprite != null)
                _itemImage.sprite = _backgroundSprite;
            if (_priceLabel != null)
                _priceLabel.text = _price.ToString();

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
                _button.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            _controller?.OnSlotClicked(this);
        }

        // Re-reads owned/selected state from the controller and toggles children.
        public void RefreshVisual()
        {
            if (_controller == null)
                return;

            var owned = _unlock == UnlockKind.Free || _controller.IsUnlocked(this);
            var locked = !owned;
            var selected = owned && _controller.IsSelected(this);

            SetActive(_lockObject, locked);
            SetActive(_coinObject, locked && _unlock == UnlockKind.Paid);
            SetActive(_addObject, locked && _unlock == UnlockKind.Ad);
            SetActive(_checkObject, selected);
            SetActive(_priceLabel != null ? _priceLabel.gameObject : null, locked && _unlock == UnlockKind.Paid);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }
    }
}
