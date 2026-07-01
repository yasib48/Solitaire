using UnityEngine;
using UnityEngine.EventSystems;

namespace Solitaire.Presenters
{
    /// <summary>
    ///     Clears the UI selection as soon as nothing is being pressed, so buttons
    ///     don't stay stuck in their Highlighted/Selected tint after a tap (the
    ///     "the color takes forever to revert" issue on touch and mouse). Attach to
    ///     the EventSystem. Safe here because the game has no input fields that need
    ///     to keep focus.
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class AutoDeselectUI : MonoBehaviour
    {
        private EventSystem _eventSystem;

        private void Awake()
        {
            _eventSystem = GetComponent<EventSystem>();
        }

        private void Update()
        {
            if (_eventSystem.currentSelectedGameObject == null)
                return;

            // Only drop the selection once the press is released, so the button
            // still shows its Pressed tint while the finger/mouse is held down.
            if (!Input.GetMouseButton(0) && Input.touchCount == 0)
                _eventSystem.SetSelectedGameObject(null);
        }
    }
}
