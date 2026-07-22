using UnityEngine;

namespace Solitaire.Presenters
{
    // Scales a background SpriteRenderer so it always fully covers the camera
    // view on any screen aspect (no gaps). Put it on a GameObject that has a
    // SpriteRenderer, place it behind the cards (sorting order well below them),
    // and assign your background sprite. Works with the fit-box camera: it
    // re-fits whenever the screen size or the camera's zoom changes.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundFitter : MonoBehaviour
    {
        [SerializeField]
        private Camera _camera;

        private SpriteRenderer _renderer;
        private Vector2Int _lastScreen;
        private float _lastOrthoSize;
        private Sprite _lastSprite;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_camera == null)
                _camera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;

            // Only refit when something that affects framing actually changed
            // (screen size, camera zoom, or the sprite itself — e.g. a background
            // swapped from the customize screen).
            if (
                Screen.width == _lastScreen.x
                && Screen.height == _lastScreen.y
                && Mathf.Approximately(_camera.orthographicSize, _lastOrthoSize)
                && _renderer.sprite == _lastSprite
            )
                return;

            _lastSprite = _renderer.sprite;

            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastOrthoSize = _camera.orthographicSize;
            Fit();
        }

        private void Fit()
        {
            if (_renderer.sprite == null)
                return;

            var worldHeight = _camera.orthographicSize * 2f;
            var worldWidth = worldHeight * _camera.aspect;

            var spriteSize = _renderer.sprite.bounds.size; // world units at scale 1
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            // Cover: pick the larger ratio so the sprite spills past every edge
            var scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y);
            transform.localScale = new Vector3(scale, scale, 1f);

            // Center on the camera (keep our own z so we stay behind the cards)
            var camPos = _camera.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
        }
    }
}
