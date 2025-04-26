using UnityEngine;

/// <summary>
/// Controls the visibility of on-screen control elements based on system settings
/// </summary>
/// <inheritdoc />
public class ControlsVisibility : MonoBehaviour
{
    [SerializeField] [Tooltip("Container holding all control UI elements")] private GameObject controlsContainer;

    // Subscribes to visibility events and sets initial state
    private void OnEnable()
    {
        UpdateVisibility(OnScreenControlsManager.ShowMultiplayerControls);

        OnScreenControlsManager.OnControlsVisibilityChanged += UpdateVisibility;
    }

    // Unsubscribes from visibility events when disabled
    private void OnDisable()
    {
        OnScreenControlsManager.OnControlsVisibilityChanged -= UpdateVisibility;
    }

    // Updates the visibility of control elements based on the provided state
    private void UpdateVisibility(bool showControls)
    {
        if (controlsContainer != null)
            controlsContainer.SetActive(showControls);
    }
}
