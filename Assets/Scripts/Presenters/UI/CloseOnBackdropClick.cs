using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Makes a full-screen backdrop act as a tap-to-close "deadzone": clicking
    ///     anywhere it receives the raycast closes the given <see cref="SlideUpPanel" />
    ///     (slide down + deactivate). Attach to the outer "Play" panel, which needs a
    ///     raycast-target Graphic (e.g. an Image, transparent or dimmed) covering the
    ///     area.
    ///
    ///     Only the top-most raycast target gets the click, so:
    ///     - Buttons (raycast target on) are NOT affected — they handle their own click.
    ///     - The inner panel closes taps too if its background blocks raycasts; set the
    ///       inner background's "Raycast Target" OFF if you want taps on empty panel
    ///       area to close it as well (everything but the buttons).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class CloseOnBackdropClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SlideUpPanel _panel;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_panel != null)
                _panel.Hide();
        }
    }
}
