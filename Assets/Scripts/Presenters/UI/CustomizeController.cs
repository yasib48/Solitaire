using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Drives the Customize (background chooser) screen. Put this on the parent
    ///     object of the slots. It owns the shared state — which backgrounds are
    ///     owned and which one is selected (both persisted) — applies the selected
    ///     background to the game, and handles the buy popup + ad unlock.
    ///
    ///     Everything scene-facing is a serialized reference so it can be wired by
    ///     hand in the inspector.
    /// </summary>
    public class CustomizeController : MonoBehaviour
    {
        private const string StorageKey = "Customize";

        [Header("Background apply target (assign one)")]
        [Tooltip("World background renderer to swap when a slot is selected (e.g. the BackgroundFitter's SpriteRenderer).")]
        [SerializeField] private SpriteRenderer _backgroundRenderer;

        [Tooltip("UI Image background to swap, if the game background is a UI image instead.")]
        [SerializeField] private Image _backgroundImage;

        [Header("Buy popup (for Paid slots)")]
        [Tooltip("The popup root shown when buying a paid background.")]
        [SerializeField] private GameObject _buyPopup;

        [Tooltip("Image inside the popup that shows the slot's background sprite.")]
        [SerializeField] private Image _buyPopupItemImage;

        [Tooltip("The count/price label on the popup's buy button.")]
        [SerializeField] private TMP_Text _buyPopupPriceLabel;

        [Tooltip("The buy button inside the popup.")]
        [SerializeField] private Button _buyButton;

        [Tooltip("Optional close/back button on the popup.")]
        [SerializeField] private Button _buyPopupCloseButton;

        [Header("Purchase feel")]
        [Tooltip("How long to keep the level bar open after a purchase so the coins visibly drain before it slides away. Match this to the coin count-down duration.")]
        [SerializeField] private float _coinDrainSeconds = 0.75f;

        [Inject] private readonly ICurrencyService _currencyService;
        [Inject] private readonly IStorageService _storageService;

        private readonly HashSet<string> _owned = new();
        private string _selectedId = "";
        private CustomizeSlotPresenter[] _slots;
        private CustomizeSlotPresenter _pendingPurchase;

        // The Level Bar is a single shared object driven centrally by
        // MainUiPresenter (via its UiSlidePanel). Rather than tween it ourselves
        // - which fought that owner and only opened it partway - we just raise a
        // request flag and let MainUiPresenter slide it in exactly like the Play
        // menu does, so the drop distance and easing match.
        private MainUiPresenter _mainUi;

        private void Start()
        {
            Load();

            _slots = GetComponentsInChildren<CustomizeSlotPresenter>(true);
            for (var i = 0; i < _slots.Length; i++)
                _slots[i].Initialize(this);

            ApplySelectedBackground();
            RefreshAllSlots();

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(OnBuyClicked);
                _buyButton.onClick.AddListener(OnBuyClicked);
            }
            if (_buyPopupCloseButton != null)
            {
                _buyPopupCloseButton.onClick.RemoveListener(CloseBuyPopup);
                _buyPopupCloseButton.onClick.AddListener(CloseBuyPopup);
            }

            SetActive(_buyPopup, false);
            SetLevelBarRequest(false);
        }

        public bool IsUnlocked(CustomizeSlotPresenter slot)
        {
            return slot.Unlock == CustomizeSlotPresenter.UnlockKind.Free || _owned.Contains(slot.BackgroundId);
        }

        public bool IsSelected(CustomizeSlotPresenter slot)
        {
            return _selectedId == slot.BackgroundId;
        }

        public void OnSlotClicked(CustomizeSlotPresenter slot)
        {
            if (IsUnlocked(slot))
            {
                Select(slot);
                return;
            }

            switch (slot.Unlock)
            {
                case CustomizeSlotPresenter.UnlockKind.Ad:
                    // Locked + ad: go straight to the ad; unlock on reward.
                    PlayRewardedAd(() => Unlock(slot));
                    break;

                case CustomizeSlotPresenter.UnlockKind.Paid:
                    // Locked + paid: open the buy popup for this slot.
                    OpenBuyPopup(slot);
                    break;
            }
        }

        private void Select(CustomizeSlotPresenter slot)
        {
            _selectedId = slot.BackgroundId;
            ApplyBackground(slot.BackgroundSprite);
            Persist();
            RefreshAllSlots();
        }

        // Unlock only — does NOT auto-select. The player clicks the now-unlocked
        // slot to actually apply it (that's when the check turns on).
        private void Unlock(CustomizeSlotPresenter slot)
        {
            if (!string.IsNullOrEmpty(slot.BackgroundId))
                _owned.Add(slot.BackgroundId);
            Persist();
            slot.RefreshVisual();
        }

        private void OpenBuyPopup(CustomizeSlotPresenter slot)
        {
            _pendingPurchase = slot;

            if (_buyPopupItemImage != null)
                _buyPopupItemImage.sprite = slot.BackgroundSprite;
            if (_buyPopupPriceLabel != null)
                _buyPopupPriceLabel.text = slot.Price.ToString();

            SetActive(_buyPopup, true);
            SetLevelBarRequest(true); // slide the level bar in so we can watch the coins
        }

        // Cancel (close button): drop the popup and slide the level bar away.
        private void CloseBuyPopup()
        {
            _pendingPurchase = null;
            SetActive(_buyPopup, false);
            SetLevelBarRequest(false);
        }

        private void OnBuyClicked()
        {
            if (_pendingPurchase == null)
                return;

            // TrySpend deducts the coins (the on-screen count animates down smoothly
            // via the currency display binding). Fails silently if too poor.
            if (_currencyService.TrySpend(_pendingPurchase.Price))
            {
                Unlock(_pendingPurchase);
                _pendingPurchase = null;
                SetActive(_buyPopup, false);
                // Keep the coins panel open while the balance visibly drains, then
                // slide it up and away once it has settled.
                FinishPurchaseAsync().Forget();
            }
        }

        private async UniTask FinishPurchaseAsync()
        {
            // Keep the level bar open while the coin balance visibly drains, then
            // let it slide away.
            await UniTask.Delay((int)(Mathf.Max(0f, _coinDrainSeconds) * 1000f), DelayType.UnscaledDeltaTime);
            SetLevelBarRequest(false);
        }

        // Ask MainUiPresenter to hold the shared Level Bar open (or release it).
        // It reconciles the actual slide every frame via its UiSlidePanel, so the
        // motion matches the Play menu exactly.
        private void SetLevelBarRequest(bool open)
        {
            if (_mainUi == null)
                _mainUi = FindObjectOfType<MainUiPresenter>();
            if (_mainUi != null)
                _mainUi.ExternalLevelBarOpenRequest = open;
        }

        private void ApplySelectedBackground()
        {
            if (string.IsNullOrEmpty(_selectedId) || _slots == null)
                return;

            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i].BackgroundId == _selectedId)
                {
                    ApplyBackground(_slots[i].BackgroundSprite);
                    return;
                }
        }

        private void ApplyBackground(Sprite sprite)
        {
            if (sprite == null)
                return;
            if (_backgroundRenderer != null)
                _backgroundRenderer.sprite = sprite;
            if (_backgroundImage != null)
                _backgroundImage.sprite = sprite;
        }

        private void RefreshAllSlots()
        {
            if (_slots == null)
                return;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i].RefreshVisual();
        }

        // Hook for a real rewarded ad. Only banner ads exist today, so for now the
        // reward is granted immediately. Replace the body with a rewarded-ad call
        // that invokes onReward on completion.
        private void PlayRewardedAd(Action onReward)
        {
            onReward?.Invoke();
        }

        private void Persist()
        {
            _storageService.Save(StorageKey, new SaveData
            {
                Owned = new List<string>(_owned),
                Selected = _selectedId
            });
        }

        private void Load()
        {
            var data = _storageService.Load<SaveData>(StorageKey);
            _owned.Clear();
            if (data != null)
            {
                if (data.Owned != null)
                    for (var i = 0; i < data.Owned.Count; i++)
                        if (!string.IsNullOrEmpty(data.Owned[i]))
                            _owned.Add(data.Owned[i]);
                _selectedId = data.Selected ?? "";
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        [Serializable]
        private class SaveData
        {
            public List<string> Owned = new();
            public string Selected = "";
        }
    }
}
