using UnityEngine;

namespace Solitaire.Presenters
{
    // Insets a RectTransform to the device safe area (notches, punch-holes,
    // rounded corners). Put it on a full-screen panel under your Canvas and
    // parent your UI (buttons, score, etc.) inside it.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreen;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Re-apply on rotation / resolution / safe-area changes
            if (
                Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreen.x
                || Screen.height != _lastScreen.y
            )
                Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            // Convert the safe-area pixel rect into normalized anchors
            var anchorMin = _lastSafeArea.position;
            var anchorMax = _lastSafeArea.position + _lastSafeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Guard against bad values before the device reports a real safe area
            if (anchorMin.x < 0 || anchorMin.y < 0 || anchorMax.x > 1 || anchorMax.y > 1)
                return;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
