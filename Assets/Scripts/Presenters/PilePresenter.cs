using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    public class PilePresenter : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField]
        private Pile.PileType Type;

        [SerializeField]
        private Pile.CardArrangement Arrangement;

        [SerializeField]
        private Vector3 PosPortrait;

        [SerializeField]
        private Vector3 PosLandscape;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly OrientationState _orientation;

        [Inject]
        private readonly Pile _pile;

        // On wider screens (tablets) the columns spread out horizontally so the
        // board isn't a narrow strip in the middle. Phones at or below the
        // reference aspect keep their designed spacing; wider screens get more.
        private const float SpreadReferenceAspect = 0.60f;
        private const float MaxSpread = 1.45f;

        private Vector2Int _lastScreen;

        // Portrait layout uses the position the pile was authored at in the
        // scene, so whatever spacing looks good in the editor is what ships.
        // PosPortrait is no longer the source of truth (it drifted from the
        // scene); only PosLandscape still drives the rotated layout.
        // ponytail: authored scene transform is the source of truth, no per-pile data to keep in sync
        private Vector3 _authoredPos;

        public Pile Pile => _pile;

        private void Awake()
        {
            _authoredPos = transform.position;
            _pile.Init(Type, Arrangement, transform.position);
        }

        private void Start()
        {
            // Update layout on orientation change
            _orientation.State.Subscribe(UpdateLayout).AddTo(this);
        }

        private void Update()
        {
            // Re-apply layout when the screen size changes (rotation, resize) so
            // the responsive column spacing follows the current screen width.
            if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
            {
                _lastScreen = new Vector2Int(Screen.width, Screen.height);
                UpdateLayout(_orientation.State.Value);
            }
        }

        // Drop is resolved by the dragged card via overlap detection (see
        // CardPresenter.FindOverlapPile), so the pile's pointer-based drop is a
        // no-op. Empty piles are still detected because they keep their collider.
        public void OnDrop(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData?.clickCount == 1 && _pile.IsStock)
                _game.RefillStock();
        }

        private void UpdateLayout(Orientation orientation)
        {
            var position = orientation == Orientation.Landscape ? PosLandscape : _authoredPos;

            // Widen the horizontal gaps based on screen width (portrait only)
            position.x *= HorizontalSpread(orientation);

            transform.position = position;
            _pile.UpdatePosition(position);
        }

        private static float HorizontalSpread(Orientation orientation)
        {
            if (orientation != Orientation.Portrait || Screen.height <= 0)
                return 1f;

            var aspect = (float)Screen.width / Screen.height;
            return Mathf.Clamp(aspect / SpreadReferenceAspect, 1f, MaxSpread);
        }
    }
}
