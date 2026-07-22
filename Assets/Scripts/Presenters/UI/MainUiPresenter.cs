using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Wires the existing menu, popup, daily reward and shop UI objects in the
    ///     Game scene. This intentionally does not create missing flows such as real
    ///     IAP or rewarded ads; it only connects screens that already exist.
    /// </summary>
    public class MainUiPresenter : MonoBehaviour
    {
        private const int DailyGiftCoins = 20;
        private const int LevelRewardCoins = 10;
        private const string DailyLoginKey = "DailyLogin";
        // Hard ceiling for the end-of-game XP reveal. Comfortably longer than the
        // real animation (a few seconds); if it's exceeded, something stalled and
        // we bail to the results screen so the game can't lock up.
        private const float GameEndAnimationTimeoutSeconds = 12f;

        [Inject] private readonly Game _game;
        [Inject] private readonly ICurrencyService _currencyService;
        [Inject] private readonly IStorageService _storageService;

        private GameObject _playPanel;
        private GameObject _settingsPanel;
        private GameObject _customizePanel;
        private GameObject _settingsButton;
        private GameObject _customizeButton;
        private GameObject _newGamePanel;
        private GameObject _areYouSurePanel;
        private GameObject _giftBoxPanel;
        private GameObject _dailyChallengePanel;
        private DailyChallengePanelPresenter _dailyChallengePresenter;
        private GameObject _giftAdBadge1;
        private GameObject _giftAdBadge2;
        private GameObject _giftAdBadge3;
        private DailyGiftBoxPresenter _giftBoxes;
        private bool _pendingReturnToGiftBox;
        private int _pendingGiftBoxIndex;
        private bool _freeDailyGiftClaimed;
        private GameObject _prizeImagePanel;
        private GameObject _prizeOpenPanel;
        private PrizeOpenPanelPresenter _prizeOpenPresenter;
        private GameObject _buyPanel;
        private GameObject[] _shortcutBlockingPanels;
        private GameObject _background;
        private GameObject _levelBar;
        private LevelBarPresenter _levelBarPresenter;
        private PrizeMarkerPresenter _prizeMarker;
        private CoinFlyRewardPresenter _coinFlyPresenter;
        private RectTransform _coinFlyRoot;
        private RectTransform _coinFlyTarget;
        private Image _coinFlyTemplate;
        private StarFlyRewardPresenter _starFlyPresenter;
        private TMP_Text _resultScoreYou;
        private TMP_Text _resultTimeYou;
        private TMP_Text _resultMovesYou;
        private TMP_Text _resultScoreBest;
        private TMP_Text _resultTimeBest;
        private TMP_Text _resultMovesBest;
        // The "Best" column (header + the three value cells) and the "NEW BEST"
        // badge graphics that replace it when the player sets a new record.
        private GameObject _resultBestHeader;
        private GameObject[] _resultBestCells;
        private GameObject[] _resultNewBestBadges;
        private bool _coinAwardInProgress;
        private bool _pendingOpenNewGameAfterPrize;
        private bool _celebratingLevelUp;
        private GameObject _activePanel;
        private readonly Dictionary<Transform, Vector3> _baseScales = new Dictionary<Transform, Vector3>();

        private void Start()
        {
            CachePanels();
            CloseAllPanelsAtStartup();
            BindNavigation();
            BindPlayMenu();
            BindNewGamePanels();
            BindDailyReward();
            BindPrizePanels();
            BindShopShell();
            BindGameEndFlow();
            BindCurrencyDisplay();
            ShowDailyGiftIfFirstLoginToday();
        }

        private void LateUpdate()
        {
            SyncShortcutButtonsVisibility();
            SyncLevelBarVisibility();
        }


        private void CachePanels()
        {
            _playPanel = FindPath("Canvas/Play");
            _settingsPanel = FindPath("Canvas/Settings");
            _customizePanel = FindPath("Canvas/Customize");
            _settingsButton = FindPath("Canvas/Settings Button");
            _customizeButton = FindPath("Canvas/Customize Button");
            _newGamePanel = FindPath("Canvas/New Game Panel");
            _areYouSurePanel = FindPath("Canvas/Are you sure Panel");
            _giftBoxPanel = FindPath("Canvas/Gift box Panel");
            _dailyChallengePanel = FindPath("Canvas/Daily Panel");
            _dailyChallengePresenter = _dailyChallengePanel != null
                ? _dailyChallengePanel.GetComponent<DailyChallengePanelPresenter>()
                : null;
            _dailyChallengePresenter?.Initialize(_game, _storageService, () => Hide(_dailyChallengePanel));
            _giftAdBadge1 = FindPath("Canvas/Gift box Panel/Image/Button/Image (2)");
            _giftAdBadge2 = FindPath("Canvas/Gift box Panel/Image/Button (1)/Image");
            _giftAdBadge3 = FindPath("Canvas/Gift box Panel/Image/Button (2)/Image (1)");
            _giftBoxes = _giftBoxPanel != null ? _giftBoxPanel.GetComponentInChildren<DailyGiftBoxPresenter>(true) : null;
            _prizeImagePanel = FindPath("Canvas/Prize Image");
            _prizeOpenPanel = FindPath("Canvas/Prize Open Panel");
            _prizeOpenPresenter = _prizeOpenPanel != null ? _prizeOpenPanel.GetComponentInChildren<PrizeOpenPanelPresenter>(true) : null;
            if (_prizeOpenPresenter == null && _prizeOpenPanel != null)
                _prizeOpenPresenter = _prizeOpenPanel.AddComponent<PrizeOpenPanelPresenter>();
            _buyPanel = FindPath("Canvas/Buy Panel");
            _shortcutBlockingPanels = new[]
            {
                _playPanel,
                _settingsPanel,
                _customizePanel,
                _newGamePanel,
                _areYouSurePanel,
                _giftBoxPanel,
                _dailyChallengePanel,
                _prizeImagePanel,
                _prizeOpenPanel,
                _buyPanel
            };
            _background = FindPath("Canvas/Background");
            _levelBar = FindPath("Canvas/Level Bar");
            CacheCoinFly();
            CacheStarFly();
            CacheResultsTable();
            _levelBarPresenter = _levelBar != null ? _levelBar.GetComponent<LevelBarPresenter>() : null;
            _prizeMarker = _prizeImagePanel != null
                ? _prizeImagePanel.GetComponentInChildren<PrizeMarkerPresenter>(true)
                : null;
        }

        private void BindNavigation()
        {
            Bind("Canvas/Bottom Bar/Settings", () => ShowOnly(_settingsPanel));
            Bind("Canvas/Bottom Bar/Daily", () => ShowOnly(_dailyChallengePanel));
            Bind("Canvas/Bottom Bar/Play", () => ShowOnly(_playPanel));
            Bind("Canvas/Level Bar/Coin Counter", () => ShowOnly(_buyPanel));
            Bind("Canvas/Settings/Top Panel/Back", () => Hide(_settingsPanel));
            Bind("Canvas/Buy Panel/Top Panel/Back", () => Hide(_buyPanel));
            Bind("Canvas/Daily Panel/Top Panel/Back", () => Hide(_dailyChallengePanel));
            Bind("Canvas/Daily Panel/Image/Back", () => Hide(_dailyChallengePanel));
            Bind("Canvas/Daily Panel/Back", () => Hide(_dailyChallengePanel));

            // In-game shortcut buttons that open their panels, plus the Customize
            // panel's own back button to close it again.
            Bind("Canvas/Settings Button", () => ShowOnly(_settingsPanel));
            Bind("Canvas/Customize Button", () => ShowOnly(_customizePanel));
            Bind("Canvas/Customize/Top Panel/Back", () => Hide(_customizePanel));
        }

        private void BindPlayMenu()
        {
            Bind("Canvas/Play/Menu Panel/Daily", () =>
            {
                Hide(_playPanel);
                ShowOnly(_dailyChallengePanel);
            });

            Bind("Canvas/Play/Menu Panel/New game", () =>
            {
                Hide(_playPanel);
                ShowOnly(_areYouSurePanel);
            });

            Bind("Canvas/Play/Menu Panel/Retry", () =>
            {
                Hide(_playPanel);
                _game.RestartCommand.Execute();
            });
        }

        private void BindNewGamePanels()
        {
            Bind("Canvas/Are you sure Panel/Image/Button", () => Hide(_areYouSurePanel));
            Bind("Canvas/Are you sure Panel/Image/Button (1)", () =>
            {
                Hide(_areYouSurePanel);
                _game.NewMatchCommand.Execute();
            });

            // No standalone home/main-menu screen exists yet, so for now the
            // "home" icon on the victory dialog surfaces the Play menu instead.
            Bind("Canvas/New Game Panel/Main Panel/Dialog Box/Home Button", () =>
            {
                Hide(_newGamePanel);
                _game.ContinueCommand.Execute();
                ShowOnly(_playPanel);
            });

            Bind("Canvas/New Game Panel/Main Panel/Dialog Box/New Game Button", () =>
            {
                Hide(_newGamePanel);
                _game.NewMatchCommand.Execute();
            });
        }

        private void BindDailyReward()
        {
            Bind("Canvas/Gift box Panel/Image/Back", () => Hide(_giftBoxPanel));
            Bind("Canvas/Gift box Panel/Image/Button", () => OpenGiftBoxPrize(0));
            Bind("Canvas/Gift box Panel/Image/Button (1)", () => OpenGiftBoxPrize(1));
            Bind("Canvas/Gift box Panel/Image/Button (2)", () => OpenGiftBoxPrize(2));
            SyncGiftAdBadges();
        }

        private void BindPrizePanels()
        {
            Bind("Canvas/Prize Open Panel/Button", () =>
            {
                AwardCoinsAsync(DailyGiftCoins).Forget();
                Hide(_prizeOpenPanel);

                if (!_pendingReturnToGiftBox)
                    return;

                _pendingReturnToGiftBox = false;
                _freeDailyGiftClaimed = true;
                ShowOnly(_giftBoxPanel);
                SetGiftBoxOpened(_pendingGiftBoxIndex, true);
                SyncGiftAdBadges(true);
            });

            // "Continue" declines the multiplier gamble and just takes the flat
            // base reward (no multiplier), then closes the prize.
            Bind("Canvas/Prize Image/BG/Continue", () =>
            {
                AwardCoinsAndCloseLevelPrizeAsync(LevelRewardCoins).Forget();
            });

            // The reward buttons grant the base reward times whatever multiplier the
            // marker triangle is currently pointing at. "With Add Get" is where a
            // rewarded-ad gate will live later; for now it grants immediately, same
            // as the no-ad "Without Add Get" path.
            Bind("Canvas/Prize Image/BG/Without Add Get", () => ClaimMultipliedReward());
            Bind("Canvas/Prize Image/BG/With Add Get", () =>
            {
                // TODO: show a rewarded ad here; grant on ad-complete instead.
                ClaimMultipliedReward();
            });
        }

        private void BindShopShell()
        {
            Bind("Canvas/Buy Panel/Scroll View/Viewport/Content/Image", () => Debug.Log("Remove-ads purchase is not implemented yet."));
            Bind("Canvas/Buy Panel/Scroll View/Viewport/Content/Image (1)", () => Debug.Log("Starter pack purchase is not implemented yet."));
        }

        private void OpenGiftBoxPrize(int boxIndex)
        {
            OpenDailyPrizeAsync(boxIndex).Forget();
        }


        private async UniTask OpenDailyPrizeAsync(int boxIndex)
        {
            if (IsGiftBoxOpened(boxIndex))
                return;
            _pendingReturnToGiftBox = true;
            _pendingGiftBoxIndex = boxIndex;
            ConfigurePrizeOpenPanelForBox(boxIndex);
            _prizeOpenPresenter?.PrepareClosedState();
            Hide(_giftBoxPanel);
            ShowOnly(_prizeOpenPanel);

            if (_prizeOpenPresenter != null)
                await _prizeOpenPresenter.PlayAsync();
        }

        private void ConfigurePrizeOpenPanelForBox(int boxIndex)
        {
            if (_prizeOpenPresenter == null || _giftBoxes == null)
                return;

            if (_giftBoxes.TryGetSprites(boxIndex, out var closedSprite, out var openedSprite))
                _prizeOpenPresenter.ConfigureSprites(closedSprite, openedSprite);
        }

        private bool IsGiftBoxOpened(int index)
        {
            return _giftBoxes != null && _giftBoxes.IsOpened(index);
        }

        private void SetGiftBoxOpened(int index, bool animate)
        {
            _giftBoxes?.SetOpened(index, true, animate);
        }

        private void SyncGiftAdBadges(bool animate = false)
        {
            if (_giftBoxes != null && _giftBoxes.BoxCount > 0)
            {
                _giftBoxes.SetFreeBoxClaimed(_freeDailyGiftClaimed, animate);
                return;
            }

            SetGiftAdBadge(_giftAdBadge1, false);
            SetGiftAdBadge(_giftAdBadge2, _freeDailyGiftClaimed, animate);
            SetGiftAdBadge(_giftAdBadge3, _freeDailyGiftClaimed, animate);
        }

        private void SetGiftAdBadge(GameObject badge, bool visible, bool animate = false)
        {
            if (badge == null)
                return;

            var baseScale = GetBaseScale(badge.transform);
            badge.transform.DOKill();
            var canvasGroup = EnsureCanvasGroup(badge);
            canvasGroup.DOKill();

            if (!visible)
            {
                badge.SetActive(false);
                badge.transform.localScale = baseScale;
                canvasGroup.alpha = 1f;
                return;
            }

            badge.SetActive(true);
            if (!animate)
            {
                badge.transform.localScale = baseScale;
                canvasGroup.alpha = 1f;
                return;
            }

            badge.transform.localScale = ScaleBy(baseScale, 0.84f);
            canvasGroup.alpha = 0f;
            badge.transform.DOScale(baseScale, 0.2f).SetEase(Ease.OutBack);
            canvasGroup.DOFade(1f, 0.14f).SetEase(Ease.OutQuad);
        }

        private void ShowOnly(GameObject panel)
        {
            OpenPanel(panel);
        }

        // The Settings/Customize shortcut buttons live on the bare game board.
        // They ride along with the "close all UIs" flow: hidden while any panel
        // is open, shown again once we're back on the board.
        private void SetShortcutButtonsVisible(bool visible)
        {
            if (_settingsButton != null && _settingsButton.activeSelf != visible)
                _settingsButton.SetActive(visible);
            if (_customizeButton != null && _customizeButton.activeSelf != visible)
                _customizeButton.SetActive(visible);
        }

        private void SyncShortcutButtonsVisibility()
        {
            SetShortcutButtonsVisible(!HasOpenShortcutBlockingPanel());
        }

        private bool HasOpenShortcutBlockingPanel()
        {
            if (_shortcutBlockingPanels == null)
                return false;

            for (var i = 0; i < _shortcutBlockingPanels.Length; i++)
            {
                var panel = _shortcutBlockingPanels[i];
                if (panel != null && panel.activeInHierarchy)
                    return true;
            }

            return false;
        }

        private void Hide(GameObject panel)
        {
            if (panel == null)
                return;

            if (_activePanel == panel)
                _activePanel = null;

            SyncLevelBarVisibility();
            AnimateHide(panel);
        }

        private void OpenPanel(GameObject panel)
        {
            if (panel == null)
                return;

            if (panel == _giftBoxPanel)
                SyncGiftAdBadges();

            _activePanel = panel;
            CloseOtherPanels(panel);
            // A panel is now up -> tuck the in-game shortcut buttons away so
            // they don't float over it.
            SetShortcutButtonsVisible(false);
            AnimateShow(panel);
            SyncLevelBarVisibility();
        }

        private void CloseAllPanelsAtStartup()
        {
            _activePanel = null;
            CloseOtherPanels(null);
            SyncShortcutButtonsVisibility();
            SetBackgroundVisible(false, true);
            SyncLevelBarVisibility();
        }

        private void CloseOtherPanels(GameObject except)
        {
            var panels = new[]
            {
                _playPanel,
                _settingsPanel,
                _customizePanel,
                _newGamePanel,
                _areYouSurePanel,
                _giftBoxPanel,
                _dailyChallengePanel,
                _prizeImagePanel,
                _prizeOpenPanel,
                _buyPanel
            };

            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null || panel == except)
                    continue;

                panel.transform.DOKill();
                var canvasGroup = EnsureCanvasGroup(panel);
                canvasGroup.DOKill();
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                panel.SetActive(false);
            }

            SyncLevelBarVisibility();
        }

        private void AnimateShow(GameObject panel)
        {
            var baseScale = GetBaseScale(panel.transform);
            panel.transform.DOKill();
            var canvasGroup = EnsureCanvasGroup(panel);
            canvasGroup.DOKill();

            panel.SetActive(true);
            panel.transform.localScale = ScaleBy(baseScale, 0.92f);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            panel.transform.DOScale(baseScale, 0.22f).SetEase(Ease.OutBack);
            canvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private void AnimateHide(GameObject panel)
        {
            var baseScale = GetBaseScale(panel.transform);
            panel.transform.DOKill();
            var canvasGroup = EnsureCanvasGroup(panel);
            canvasGroup.DOKill();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            panel.transform.DOScale(ScaleBy(baseScale, 0.94f), 0.14f).SetEase(Ease.InBack);
            canvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                panel.SetActive(false);
                panel.transform.localScale = baseScale;
                SyncShortcutButtonsVisibility();
            });
        }

        private Vector3 GetBaseScale(Transform target)
        {
            if (target == null)
                return Vector3.one;

            if (!_baseScales.TryGetValue(target, out var baseScale))
            {
                baseScale = target.localScale;
                _baseScales.Add(target, baseScale);
            }

            return baseScale;
        }

        private static Vector3 ScaleBy(Vector3 baseScale, float multiplier)
        {
            return new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject panel)
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = panel.AddComponent<CanvasGroup>();

            return canvasGroup;
        }

        private void SetBackgroundVisible(bool visible, bool immediate = false)
        {
            if (_background == null)
                _background = FindPath("Canvas/Background");
            if (_background == null)
                return;

            PrepareBackgroundForLevelFlow();

            _background.transform.DOKill();
            var canvasGroup = EnsureCanvasGroup(_background);
            canvasGroup.DOKill();
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (visible)
            {
                _background.SetActive(true);
                if (_levelBar != null)
                    _levelBar.transform.SetAsLastSibling();
                if (immediate)
                {
                    canvasGroup.alpha = 1f;
                    return;
                }

                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
                return;
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (immediate)
            {
                canvasGroup.alpha = 0f;
                _background.SetActive(false);
                return;
            }

            canvasGroup.DOFade(0f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                _background.SetActive(false);
            });
        }

        private void PrepareBackgroundForLevelFlow()
        {
            var rect = _background.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            var image = _background.GetComponent<Image>();
            if (image != null)
            {
                var color = image.color;
                color.r = 0f;
                color.g = 0f;
                color.b = 0f;
                if (color.a <= 0.01f)
                    color.a = 0.4f;
                image.color = color;
                image.raycastTarget = true;
            }
        }

        private void SetLevelBarVisible(bool visible)
        {
            if (_levelBar != null)
                _levelBar.SetActive(visible);
        }

        // External flows (e.g. the Customize buy popup) can request the shared
        // Level Bar slide open — using the exact same UiSlidePanel motion the
        // Play menu uses — so the player can watch their coin balance. The
        // per-frame reconciliation in SyncLevelBarVisibility honours it.
        public bool ExternalLevelBarOpenRequest { get; set; }

        private void SyncLevelBarVisibility()
        {
            var shownInPlayMenu = _activePanel == _playPanel && _playPanel != null && _playPanel.activeSelf;
            var shouldBeOpen = _coinAwardInProgress || _celebratingLevelUp || shownInPlayMenu || ExternalLevelBarOpenRequest;

            if (_levelBarPresenter == null)
            {
                SetLevelBarVisible(shouldBeOpen);
                return;
            }

            // UiSlidePanel's Open/Close calls are idempotent no-ops once the
            // panel is already in the requested state, so it's safe to call
            // this every frame (see LateUpdate) without fighting an in-flight
            // slide animation or forcing an instant, un-animated SetActive.
            if (shouldBeOpen == _levelBarPresenter.IsVisible)
                return;

            if (shouldBeOpen)
                _levelBarPresenter.SlideInAsync().Forget();
            else
                _levelBarPresenter.SlideOutAsync().Forget();
        }

        private void ClaimMultipliedReward()
        {
            var multiplier = _prizeMarker != null ? Mathf.Max(1, _prizeMarker.CurrentMultiplier) : 1;
            _prizeMarker?.Freeze();
            AwardCoinsAndCloseLevelPrizeAsync(LevelRewardCoins * multiplier).Forget();
        }



        private async UniTask AwardCoinsAndCloseLevelPrizeAsync(int amount)
        {
            await AwardCoinsAsync(amount);
            CloseLevelPrize();
        }

        private async UniTask AwardCoinsAsync(int amount)
        {
            if (amount <= 0)
                return;

            _coinAwardInProgress = true;

            try
            {
                if (_levelBarPresenter != null)
                    await _levelBarPresenter.SlideInAsync();
                else
                    SyncLevelBarVisibility();

                if (_coinFlyPresenter != null)
                    await _coinFlyPresenter.PlayAsync(amount);

                _currencyService.Add(amount);

                // Adding to the balance kicks off the smooth count-up on the coin
                // label (CoinCountDuration). Wait for it to actually finish - plus
                // a short beat so the final number is readable - before the finally
                // block slides the bar away. Otherwise it closed just as the count
                // was starting to climb.
                await UniTask.Delay(
                    (int)((CoinCountDuration + CoinSettleHoldSeconds) * 1000f),
                    DelayType.UnscaledDeltaTime);
            }
            finally
            {
                _coinAwardInProgress = false;

                var shownInPlayMenu = _activePanel == _playPanel && _playPanel != null && _playPanel.activeSelf;
                var shouldStayVisible = _celebratingLevelUp || shownInPlayMenu;

                if (_levelBarPresenter != null)
                {
                    // Either resolves to the settled-open state (no-op if
                    // already open) or fully slides closed - never an instant
                    // snap either way.
                    if (shouldStayVisible)
                        await _levelBarPresenter.SlideInAsync();
                    else
                        await _levelBarPresenter.SlideOutAsync();
                }
                else
                {
                    SyncLevelBarVisibility();
                }
            }
        }

        private void CacheCoinFly()
        {
            var canvas = FindPath("Canvas");
            _coinFlyRoot = canvas != null ? canvas.transform as RectTransform : null;
            var target = FindPath("Canvas/Level Bar/Coin Counter");
            _coinFlyTarget = target != null ? target.transform as RectTransform : null;
            var template = FindPath("Canvas/Level Bar/Coin Counter/Coin Icon");
            _coinFlyTemplate = template != null ? template.GetComponent<Image>() : null;

            if (_coinFlyRoot == null || _coinFlyTarget == null || _coinFlyTemplate == null)
                return;

            _coinFlyPresenter = canvas.GetComponent<CoinFlyRewardPresenter>();
            if (_coinFlyPresenter == null)
                _coinFlyPresenter = canvas.AddComponent<CoinFlyRewardPresenter>();
            _coinFlyPresenter.Configure(_coinFlyRoot, _coinFlyTarget, _coinFlyTemplate);
        }

        // Mirror of CacheCoinFly, but flies stars up into the level bar's star
        // icon (the XP bar) at game end instead of coins into the coin counter.
        private void CacheStarFly()
        {
            var canvas = FindPath("Canvas");
            var root = canvas != null ? canvas.transform as RectTransform : null;
            var target = FindPath("Canvas/Level Bar/Level Progress Bar/Star Icon");
            var targetRect = target != null ? target.transform as RectTransform : null;
            var template = target != null ? target.GetComponent<Image>() : null;

            if (root == null || targetRect == null || template == null)
                return;

            _starFlyPresenter = canvas.GetComponent<StarFlyRewardPresenter>();
            if (_starFlyPresenter == null)
                _starFlyPresenter = canvas.AddComponent<StarFlyRewardPresenter>();
            _starFlyPresenter.Configure(root, targetRect, template);
        }

        // Called by the level bar for each XP chunk it starts adding, so stars
        // fly up into the bar while that chunk fills. Fire-and-forget so the
        // fill animation and the star flight run together.
        private void PlayStarFlyForSegment(int xp)
        {
            if (_starFlyPresenter != null && xp > 0)
                _starFlyPresenter.PlayAsync(xp).Forget();
        }

        private readonly List<TMP_Text> _coinLabels = new List<TMP_Text>();
        private int _displayedBalance;
        private Tweener _coinCountTween;
        private const float CoinCountDuration = 0.6f;
        // Extra beat to hold the settled coin total on screen after the count-up
        // finishes, before the level bar slides away.
        private const float CoinSettleHoldSeconds = 0.4f;

        // Binds every "Coin Amount" label in the UI to the real, persisted coin
        // balance so the on-screen count reflects money actually earned/saved
        // rather than a static placeholder.
        private void BindCurrencyDisplay()
        {
            var canvas = FindPath("Canvas");
            if (canvas == null)
                return;

            var labels = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
                if (labels[i] != null && labels[i].gameObject.name == "Coin Amount")
                    _coinLabels.Add(labels[i]);

            if (_coinLabels.Count == 0)
                return;

            _displayedBalance = _currencyService.Balance.Value;
            SetCoinLabels(_displayedBalance);

            _currencyService.Balance.Subscribe(AnimateCoinBalance).AddTo(this);
        }

        // Smoothly counts the coin display toward the new balance instead of
        // snapping — both up (rewards tick up) and down (a purchase visibly
        // drains). The wallet is persistent (never reset like the score), so a
        // decrease is always a real spend worth animating.
        private void AnimateCoinBalance(int target)
        {
            _coinCountTween?.Kill();

            if (target == _displayedBalance)
            {
                SetCoinLabels(target);
                return;
            }

            _coinCountTween = DOTween
                .To(() => _displayedBalance, x =>
                {
                    _displayedBalance = x;
                    SetCoinLabels(x);
                }, target, CoinCountDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void SetCoinLabels(int value)
        {
            for (var i = 0; i < _coinLabels.Count; i++)
                if (_coinLabels[i] != null)
                    _coinLabels[i].text = value.ToString();
        }

        private void CacheResultsTable()
        {
            const string root = "Canvas/New Game Panel/Main Panel/Dialog Box/Results Table/";
            _resultScoreYou = FindLabel(root + "Score You Value");
            _resultTimeYou = FindLabel(root + "Time You Value");
            _resultMovesYou = FindLabel(root + "Moves You Value");
            _resultScoreBest = FindLabel(root + "Score Best Value");
            _resultTimeBest = FindLabel(root + "Time Best Value");
            _resultMovesBest = FindLabel(root + "Moves Best Value");

            _resultBestHeader = FindPath(root + "Best Header");
            _resultBestCells = new[]
            {
                FindPath(root + "Score Best Value"),
                FindPath(root + "Time Best Value"),
                FindPath(root + "Moves Best Value")
            };
            _resultNewBestBadges = new[]
            {
                FindPath(root + "Score New Best"),
                FindPath(root + "Time New Best"),
                FindPath(root + "Moves New Best")
            };
        }

        private static TMP_Text FindLabel(string path)
        {
            var go = FindPath(path);
            return go != null ? go.GetComponent<TMP_Text>() : null;
        }

        // Fills the victory dialog's results table with this game's stats and the
        // stored records. Time shows as m:ss; a zero record reads as a dash.
        private void PopulateResultsTable(Game.GameEndResult result)
        {
            if (result == null)
                return;

            if (_resultScoreYou != null) _resultScoreYou.text = result.Points.ToString();
            if (_resultTimeYou != null) _resultTimeYou.text = FormatTime(result.TimeSeconds);
            if (_resultMovesYou != null) _resultMovesYou.text = result.Moves.ToString();

            if (_resultScoreBest != null) _resultScoreBest.text = result.BestScore > 0 ? result.BestScore.ToString() : "-";
            if (_resultTimeBest != null) _resultTimeBest.text = result.BestTimeSeconds > 0 ? FormatTime(result.BestTimeSeconds) : "-";
            if (_resultMovesBest != null) _resultMovesBest.text = result.BestMoves > 0 ? result.BestMoves.ToString() : "-";

            ApplyNewBestState(result.NewBestScore);
        }

        // When the player sets a new record, hide the whole "Best" column (its
        // header and the three value cells - the record is now just the player's
        // own score in the "You" column) and light up the "NEW BEST" badges
        // instead. Otherwise show the normal Best column and hide the badges.
        private void ApplyNewBestState(bool isNewBest)
        {
            SetGoActive(_resultBestHeader, !isNewBest);

            if (_resultBestCells != null)
                for (var i = 0; i < _resultBestCells.Length; i++)
                    SetGoActive(_resultBestCells[i], !isNewBest);

            if (_resultNewBestBadges != null)
                for (var i = 0; i < _resultNewBestBadges.Length; i++)
                    SetGoActive(_resultNewBestBadges[i], isNewBest);
        }

        private static void SetGoActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private static string FormatTime(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "0:00";
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:00}";
        }

        private void Bind(string path, System.Action action)
        {
            var button = FindPath(path)?.GetComponent<Button>();
            if (button == null || action == null)
                return;

            button.onClick.RemoveAllListeners();
            button.OnClickAsObservable().Subscribe(_ => action()).AddTo(this);
        }

        private static GameObject FindPath(string path)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < all.Length; i++)
                if (BuildPath(all[i].transform) == path)
                    return all[i];
            return null;
        }

        [Serializable]
        private class DailyLoginData
        {
            public string Date;
        }

        private static string BuildPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    

        private static void SetActive(string path, bool active)
        {
            var go = FindPath(path);
            if (go != null)
                go.SetActive(active);
        }


        private void BindGameEndFlow()
        {
            _game.GameEnded.Subscribe(HandleGameEnded).AddTo(this);
        }

        private void HandleGameEnded(Game.GameEndResult result)
        {
            ShowGameEndFlowAsync(result).Forget();
        }

        // End-of-game sequence, in order:
        //   1) Always fill the XP bar up (rolling the level over if it advanced).
        //   2) If the player leveled up, show the level-up reward popup, which
        //      then opens the New Game panel when dismissed.
        //   3) Otherwise open the New Game panel straight away.
        private async UniTask ShowGameEndFlowAsync(Game.GameEndResult result)
        {
            PopulateResultsTable(result);

            _celebratingLevelUp = true;
            SetBackgroundVisible(true);
            SyncLevelBarVisibility();

            try
            {
                // Fly stars up into the XP bar as each XP chunk is added (base,
                // combo, best), so the stars arrive while the bar is growing
                // rather than all at once beforehand.
                //
                // Guarded with a timeout + catch: this reveal is pure eye-candy,
                // and if any tween/await inside it ever fails or stalls (which
                // has happened only in player builds), the player must NOT be
                // stranded on a frozen board - we always fall through to the
                // prize / New Game panel below.
                if (_levelBarPresenter != null && result != null)
                    await _levelBarPresenter.PlayGainAsync(result, PlayStarFlyForSegment)
                        .Timeout(TimeSpan.FromSeconds(GameEndAnimationTimeoutSeconds));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"XP reveal did not finish cleanly, skipping to results: {e.Message}");
            }
            finally
            {
                _celebratingLevelUp = false;
                SetBackgroundVisible(false);
                SyncLevelBarVisibility();
            }

            if (result != null && result.LeveledUp)
            {
                ConfigureLevelPrize(result.IsFirstLevelUp);
                _pendingOpenNewGameAfterPrize = true;
                ShowOnly(_prizeImagePanel);
                return;
            }

            ShowOnly(_newGamePanel);
        }

        private void ConfigureLevelPrize(bool isFirstLevelUp)
        {
            SetActive("Canvas/Prize Image/BG/Without Add Get", isFirstLevelUp);
            SetActive("Canvas/Prize Image/BG/With Add Get", !isFirstLevelUp);
        }

        private void CloseLevelPrize()
        {
            Hide(_prizeImagePanel);
            if (!_pendingOpenNewGameAfterPrize)
                return;

            _pendingOpenNewGameAfterPrize = false;
            ShowOnly(_newGamePanel);
        }

        private void ShowDailyGiftIfFirstLoginToday()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var data = _storageService.Load<DailyLoginData>(DailyLoginKey);
            if (data != null && data.Date == today)
                return;

            _storageService.Save(DailyLoginKey, new DailyLoginData { Date = today });
            ShowOnly(_giftBoxPanel);
        }
    }
}
