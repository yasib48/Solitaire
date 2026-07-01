using System.Collections.Generic;
using System.Linq;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    public class GamePresenter : MonoBehaviour
    {
        [Header("Camera framing (world units)")]
        [SerializeField]
        private float _designHalfHeightPortrait = 8.25f;

        [SerializeField]
        private float _designHalfHeightLandscape = 4.25f;

        [Tooltip("Extra world units added to each side of the outermost piles " +
                 "(roughly a card half-width plus breathing room).")]
        [SerializeField]
        private float _horizontalPadding = 0.62f;

        [Tooltip("World units from the top of the screen to the centre of the top " +
                 "pile row (portrait). Smaller = board sits higher / closer to the HUD. " +
                 "Default 2.91 reproduces the editor pre-play framing (8.25 ortho - 5.3395 top row).")]
        [SerializeField]
        private float _topPadding = 2.91f;

        [SerializeField]
        private Physics2DRaycaster _cardRaycaster;

        [SerializeField]
        private PilePresenter _pileStock;

        [SerializeField]
        private PilePresenter _pileWaste;

        [SerializeField]
        private PilePresenter[] _pileFoundations;

        [SerializeField]
        private PilePresenter[] _pileTableaus;

        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly OrientationState _orientation;

        private Camera _camera;
        private int _layerInteractable;
        private Transform[] _pileTransforms;
        private Orientation _lastOrientation = Orientation.Unknown;
        private Vector2Int _lastScreen;
        private bool _frameDirty;
        private float _baseCameraY;

        private void Awake()
        {
            _camera = Camera.main;
            _layerInteractable = LayerMask.NameToLayer("Interactable");
            _baseCameraY = _camera.transform.position.y;
        }

        private void Start()
        {
            // Update camera on orientation change
            _orientation.State.Subscribe(AdjustCamera).AddTo(this);

            // Handle game state change
            _gameState.State.Pairwise().Subscribe(HandleGameStateChanges).AddTo(this);

            // Initialize game
            _game.Init(
                _pileStock.Pile,
                _pileWaste.Pile,
                _pileFoundations.Select(p => p.Pile).ToList(),
                _pileTableaus.Select(p => p.Pile).ToList()
            );

            SetCameraLayers(true);

            // Collect pile transforms so the camera can frame the real board width
            var piles = new List<Transform> { _pileStock.transform, _pileWaste.transform };
            foreach (var p in _pileFoundations)
                piles.Add(p.transform);
            foreach (var p in _pileTableaus)
                piles.Add(p.transform);
            _pileTransforms = piles.ToArray();
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            // Skip the home screen: deal a fresh game right away
            _game.NewMatchCommand.Execute();
        }

        private void Update()
        {
            // Re-frame the camera when the screen size changes (rotation, resize)
            if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
            {
                _lastScreen = new Vector2Int(Screen.width, Screen.height);
                _frameDirty = true;
            }

            // Detect win condition
            if (_gameState.State.Value == Game.State.Playing)
                _game.DetectWinCondition();
        }

        private void LateUpdate()
        {
            // Run after pile positions update so we read their final layout
            if (_frameDirty)
            {
                _frameDirty = false;
                FrameCamera();
            }
        }

        private void AdjustCamera(Orientation orientation)
        {
            _lastOrientation = orientation;
            _frameDirty = true;
        }

        // Fit the whole board into the camera on any screen aspect: keep the
        // designed vertical size, and zoom out if the board is wider than the
        // screen can show (narrow phones) so no columns get clipped.
        private void FrameCamera()
        {
            if (_pileTransforms == null || _pileTransforms.Length == 0)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (var i = 0; i < _pileTransforms.Length; i++)
            {
                var pos = _pileTransforms[i].position;
                if (pos.x < minX)
                    minX = pos.x;
                if (pos.x > maxX)
                    maxX = pos.x;
                if (pos.y > maxY)
                    maxY = pos.y;
            }

            var aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            var halfHeight =
                _lastOrientation == Orientation.Landscape
                    ? _designHalfHeightLandscape
                    : _designHalfHeightPortrait;
            var halfWidth = (maxX - minX) * 0.5f + _horizontalPadding;

            var orthoSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.0001f, aspect));
            _camera.orthographicSize = orthoSize;

            // Anchor the top pile row a fixed distance below the screen top instead
            // of centring the board. On tall phones the extra space then falls to
            // the bottom (empty table) rather than opening a gap under the HUD.
            var camPos = _camera.transform.position;
            camPos.y =
                _lastOrientation == Orientation.Landscape
                    ? _baseCameraY
                    : maxY + _topPadding - orthoSize;
            _camera.transform.position = camPos;
        }

        private void HandleGameStateChanges(Pair<Game.State> state)
        {
            if (state.Previous == Game.State.Home)
            {
                // Render everything and play music
                SetCameraLayers(false);
                _audioService.PlayMusic(Audio.Music, 0.3333f);
            }
            else if (state.Current == Game.State.Home)
            {
                // Cull game elements and stop music
                SetCameraLayers(true);
                _audioService.StopMusic();
            }

            // Enable card interactions only while playing
            _cardRaycaster.enabled = state.Current == Game.State.Playing;
        }

        private void SetCameraLayers(bool cullGame)
        {
            if (cullGame)
                // Every layer except Interactable
                _camera.cullingMask = ~(1 << _layerInteractable);
            else
                // Everything
                _camera.cullingMask = ~0;
        }
    }
}
