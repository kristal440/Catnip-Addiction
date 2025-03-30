using UnityEngine;

public class ControlsVisibility : MonoBehaviour
{
    [SerializeField] private GameObject controlsContainer;

    private void OnEnable()
    {
        UpdateVisibility(OnScreenControlsManager.ShowMultiplayerControls);

        OnScreenControlsManager.OnControlsVisibilityChanged += UpdateVisibility;
    }

    private void OnDisable()
    {
        OnScreenControlsManager.OnControlsVisibilityChanged -= UpdateVisibility;
    }

    private void UpdateVisibility(bool showControls)
    {
        if (controlsContainer != null)
            controlsContainer.SetActive(showControls);
    }
}
