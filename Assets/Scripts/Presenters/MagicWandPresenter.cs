using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Runtime visual effects for the magic wand. Builds a full-screen dim quad
    ///     on <see cref="Awake" /> and spawns golden glows and sparkle stars on
    ///     demand, all driven from the <see cref="Solitaire.Models.Game" /> model
    ///     through <see cref="IMagicWandVfx" />.
    ///
    ///     The board (cards) is drawn with world-space SpriteRenderers, while the
    ///     score/bottom-bar HUD lives on a Screen Space - Overlay Canvas, which
    ///     always renders on top of world-space sprites regardless of sorting
    ///     order. So dimming the board alone leaves the HUD lit.
    ///
    ///     Rather than laying a dark rectangle over the HUD (which shows up as its
    ///     own visible box against each panel's rounded background/icons), the HUD
    ///     panels are faded out to fully invisible via their own CanvasGroup and
    ///     faded back in afterwards. Nothing extra is drawn, so there's no overlay
    ///     shape to see - the icons just aren't there while the wand is active.
    /// </summary>
    public class MagicWandPresenter : MonoBehaviour, IMagicWandVfx
    {
        private const int DimSortingOrder = 2400;
        private const int GlowSortingOrder = 2600;
        private const int BeamSortingOrder = 2700;
        private const int SparkleSortingOrder = 2800;
        private const int WandSortingOrder = 2900;

        [Header("Sprites")]
        [SerializeField]
        private Sprite _glowSprite;

        [SerializeField]
        private Sprite _starSprite;

        [Tooltip("The wand icon flown around during the intro. Auto-grabbed from the Magic Wand Button if left empty.")]
        [SerializeField]
        private Sprite _wandSprite;

        [Header("Tuning")]
        [SerializeField]
        private float _dimAlpha = 0.72f;

        [SerializeField]
        private float _fadeDuration = 0.28f;

        [SerializeField]
        private Color _glowColor = new(1f, 0.86f, 0.35f, 1f);

        [SerializeField]
        private float _glowSize = 3.2f;

        [SerializeField]
        private float _slotSpacing = 3.2f;

        [Header("Reveal Tuning - live, no recompile needed")]
        [Tooltip("How big the revealed card grows while held in the centre.")]
        [SerializeField, Range(1f, 3f)]
        private float _revealScale = 2.1f;

        [Tooltip("How long (ms) a card lingers, big and glowing, in the centre.")]
        [SerializeField, Range(0, 2000)]
        private int _holdMs = 450;

        [Tooltip("How dim (0-1) the other, non-revealed cards go while the wand is active.")]
        [SerializeField, Range(0f, 1f)]
        private float _cardDimAmount = 0.58f;

        [Tooltip("Delay before the next card starts lifting, when revealing two cards.")]
        [SerializeField, Range(0, 1000)]
        private int _liftStaggerMs = 150;

        [Header("Wand Intro - live, no recompile needed")]
        [Tooltip("Size the wand grows to at the centre before it starts picking cards.")]
        [SerializeField, Range(0.5f, 6f)]
        private float _wandBigScale = 2.4f;

        [Tooltip("Size the wand shrinks to while hopping over the cards it picks.")]
        [SerializeField, Range(0.2f, 3f)]
        private float _wandSmallScale = 1.1f;

        [Tooltip("How high above each card the wand hovers, so it touches the card from above (dips down) rather than from below.")]
        [SerializeField, Range(0f, 3f)]
        private float _wandTouchYOffset = 1.6f;

        [Tooltip("How much (degrees) the wand tilts as it dips onto a card. Lower = more upright. Kept on one side so the tip always faces the card.")]
        [SerializeField, Range(0f, 45f)]
        private float _wandTiltAngle = 12f;

        [Tooltip("Extra tilt added on alternating cards for a bit of variety, so it doesn't look mechanical.")]
        [SerializeField, Range(0f, 20f)]
        private float _wandTiltVariation = 4f;

        public float RevealScale => _revealScale;
        public int HoldMs => _holdMs;
        public float CardDimAmount => _cardDimAmount;
        public int LiftStaggerMs => _liftStaggerMs;

        private static readonly string[] HudPanelNames =
        {
            "Canvas/Game Info Bar",
            "Canvas/Bottom Bar",
            "Canvas/Magic Wand Widget",
            "Canvas/Settings Button",
            "Canvas/Customize Button"
        };

        private Camera _camera;
        private SpriteRenderer _dim;
        private readonly List<CanvasGroup> _hudGroups = new();
        private Sprite _whitePixelSprite;
        private Tweener _dimTween;

        private void Awake()
        {
            _camera = Camera.main;
            _whitePixelSprite = BuildWhitePixel();
            BuildDimOverlay();
            CacheHudGroups();
        }

        public async UniTask FadeAsync(bool dim)
        {
            if (_dim == null && _hudGroups.Count == 0)
                return;

            // dim=true hides the HUD entirely (alpha 0); dim=false brings it back.
            var target = dim ? 0f : 1f;
            var done = false;

            for (var i = 0; i < _hudGroups.Count; i++)
            {
                var group = _hudGroups[i];
                group.interactable = !dim;
                group.blocksRaycasts = !dim;
                group.DOKill();
                group.DOFade(target, _fadeDuration).SetEase(Ease.Linear);
            }

            _dimTween?.Kill();
            _dimTween = _dim != null
                ? _dim.DOFade(dim ? _dimAlpha : 0f, _fadeDuration).SetEase(Ease.Linear).OnComplete(() => done = true)
                : null;

            if (_dimTween == null)
                done = true;

            await UniTask.WaitUntil(() => done);
        }

        public async UniTask WaveWandAsync(IReadOnlyList<Vector3> targets, System.Action<int> onReachTarget = null)
        {
            var sprite = ResolveWandSprite();
            if (sprite == null || _camera == null)
                return;

            var centre = _camera.transform.position;
            centre.z = 0f;

            var go = new GameObject("MagicWandCursor");
            go.transform.position = centre;
            go.transform.localScale = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // Cards render on the "Card" sorting layer, above the default one; put
            // the wand there too (high order) so it flies OVER the cards, not
            // behind them.
            sr.sortingLayerID = ResolveCardSortingLayer();
            sr.sortingOrder = WandSortingOrder;
            sr.color = new Color(1f, 1f, 1f, 0f);

            // 1) Appear at the centre and grow, with a slow wiggle.
            var appear = DOTween.Sequence().SetUpdate(true);
            appear.Append(sr.DOFade(1f, 0.16f));
            appear.Join(go.transform.DOScale(_wandBigScale, 0.4f).SetEase(Ease.OutBack));
            appear.Join(go.transform.DORotate(new Vector3(0f, 0f, -12f), 0.4f).SetEase(Ease.OutQuad));
            await appear.AsyncWaitForCompletion();
            await UniTask.Delay(140);

            // 2) Shrink, then hop over each target with a rotation, like picking it.
            var shrink = go.transform.DOScale(_wandSmallScale, 0.2f).SetEase(Ease.InOutQuad).SetUpdate(true);
            await shrink.AsyncWaitForCompletion();

            if (targets != null)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var cardPos = targets[i];
                    cardPos.z = 0f;
                    // Hover above the card so the wand reaches down onto it from
                    // the top instead of poking up from underneath.
                    var hoverPos = cardPos + Vector3.up * _wandTouchYOffset;
                    // Keep the tilt on the SAME side for every card so the wand's
                    // tip (the star end) always dips toward the card. Alternating
                    // the sign flipped the first card so it tapped with the handle
                    // (bottom) end instead. A tiny variation keeps it from looking
                    // mechanical without flipping which end touches.
                    var tilt = -(_wandTiltAngle + (i % 2 == 0 ? 0f : _wandTiltVariation));

                    var hop = DOTween.Sequence().SetUpdate(true);
                    hop.Append(go.transform.DOMove(hoverPos, 0.3f).SetEase(Ease.InOutQuad));
                    hop.Join(go.transform.DORotate(new Vector3(0f, 0f, tilt), 0.3f).SetEase(Ease.OutQuad));
                    // Dip DOWN to tap the top of the card, then back up.
                    hop.Append(go.transform.DOPunchPosition(new Vector3(0f, -0.28f, 0f), 0.18f, 6, 0.6f));
                    await hop.AsyncWaitForCompletion();

                    // The wand has "landed" on this card - light it up + sparkle.
                    onReachTarget?.Invoke(i);
                    Sparkle(cardPos);
                    await UniTask.Delay(120);
                }
            }

            // 3) Vanish.
            var vanish = DOTween.Sequence().SetUpdate(true);
            vanish.Append(sr.DOFade(0f, 0.2f));
            vanish.Join(go.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            await vanish.AsyncWaitForCompletion();

            Destroy(go);
        }

        private Sprite ResolveWandSprite()
        {
            if (_wandSprite != null)
                return _wandSprite;

            // Fallback: borrow the icon off the Magic Wand Button so the intro
            // still works even if the field wasn't wired in the inspector.
            var button = GameObject.Find("Canvas/Magic Wand Widget/Magic Wand Button");
            var image = button != null ? button.GetComponent<Image>() : null;
            if (image != null)
                _wandSprite = image.sprite;

            return _wandSprite;
        }

        private int _cardSortingLayer;
        private bool _cardSortingLayerResolved;

        // The layer the cards render on ("Card"), so wand/effects can sort above
        // them. Falls back to Default if that layer isn't defined.
        private int ResolveCardSortingLayer()
        {
            if (_cardSortingLayerResolved)
                return _cardSortingLayer;

            _cardSortingLayerResolved = true;
            _cardSortingLayer = SortingLayer.NameToID("Card");
            if (!SortingLayer.IsValid(_cardSortingLayer))
                _cardSortingLayer = 0;
            return _cardSortingLayer;
        }

        public Vector3 GetRevealSlot(int index, int count)
        {
            var centre = _camera != null ? _camera.transform.position : Vector3.zero;
            centre.z = 0f;

            var offset = (index - (count - 1) * 0.5f) * _slotSpacing;
            centre.x += offset;
            return centre;
        }

public Transform ShowGlow(Vector3 worldPos)
        {
            return null;
        }

        public void HideGlow(Transform glow)
        {
            if (glow == null)
                return;

            var sr = glow.GetComponent<SpriteRenderer>();
            glow.DOKill();
            if (sr != null)
                sr.DOFade(0f, 0.2f).OnComplete(() => Destroy(glow.gameObject));
            else
                Destroy(glow.gameObject, 0.2f);
        }

public void Sparkle(Vector3 worldPos)
        {
        }

        private void SpawnStar(Vector3 pos, float size)
        {
            var go = new GameObject("MagicWandSparkle");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _starSprite;
            sr.color = _glowColor;
            sr.sortingOrder = SparkleSortingOrder;

            go.transform.localScale = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 90f));

            var seq = DOTween.Sequence();
            seq.Append(go.transform.DOScale(size, 0.18f).SetEase(Ease.OutBack));
            seq.Join(go.transform.DORotate(new Vector3(0, 0, 180f), 0.55f, RotateMode.FastBeyond360));
            seq.Append(go.transform.DOScale(0f, 0.28f).SetEase(Ease.InBack));
            seq.Join(sr.DOFade(0f, 0.28f));
            seq.OnComplete(() => Destroy(go));
        }

        private void BuildDimOverlay()
        {
            var go = new GameObject("MagicWandDim");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(
                _camera != null ? _camera.transform.position.x : 0f,
                _camera != null ? _camera.transform.position.y : 0f,
                0.2f
            );

            _dim = go.AddComponent<SpriteRenderer>();
            _dim.sprite = _whitePixelSprite;
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.sortingOrder = DimSortingOrder;

            // Oversized so it covers the board on any aspect / ortho size.
            _dim.drawMode = SpriteDrawMode.Sliced;
            _dim.size = new Vector2(200f, 200f);
        }

        private void CacheHudGroups()
        {
            for (var i = 0; i < HudPanelNames.Length; i++)
            {
                var panel = GameObject.Find(HudPanelNames[i]);
                if (panel == null)
                    continue;

                var group = panel.GetComponent<CanvasGroup>();
                if (group == null)
                    group = panel.AddComponent<CanvasGroup>();

                _hudGroups.Add(group);
            }
        }

        private static Sprite BuildWhitePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0,
                SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }
    

private void SpawnBeam(Vector3 target, int index)
        {
            if (_whitePixelSprite == null)
                return;

            var source = GetOffscreenBeamSource(target, index);
            var delta = target - source;
            var length = delta.magnitude;
            if (length <= 0.01f)
                return;

            var go = new GameObject("MagicWandBeam");
            go.transform.position = source + delta * 0.5f;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(length, 0.06f + index * 0.015f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _whitePixelSprite;
            sr.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f);
            sr.sortingOrder = BeamSortingOrder;

            var seq = DOTween.Sequence();
            seq.Append(sr.DOFade(0.58f, 0.08f));
            seq.Join(go.transform.DOScaleY(go.transform.localScale.y * 1.8f, 0.12f).SetEase(Ease.OutQuad));
            seq.Append(sr.DOFade(0f, 0.32f));
            seq.Join(go.transform.DOScaleX(length * 1.08f, 0.32f).SetEase(Ease.OutSine));
            seq.OnComplete(() => Destroy(go));
        }

        private Vector3 GetOffscreenBeamSource(Vector3 target, int index)
        {
            if (_camera == null)
                return target + new Vector3(-8f, 5f + index, 0f);

            var viewportX = index % 2 == 0 ? -0.18f : 1.18f;
            var viewportY = 1.08f + index * 0.08f;
            var source = _camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, Mathf.Abs(_camera.transform.position.z)));
            source.z = 0f;
            return source;
        }
}
}
