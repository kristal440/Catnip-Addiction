using System;
using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Manages visibility of on-screen controls for multiplayer gameplay.
/// </summary>
public class OnScreenControlsManager : MonoBehaviour
{
    [SerializeField] [Tooltip("Toggle UI element that controls the visibility of on-screen controls")] private Toggle controlsToggle;

    internal static bool ShowMultiplayerControls { get; set; }
    public static event Action<bool> OnControlsVisibilityChanged;

    private const string MultiplayerControlsKey = "ShowMultiplayerControls";

    /// Sets visibility of multiplayer controls and triggers visibility event
    internal static void SetControlsVisibility(bool isVisible)
    {
        ShowMultiplayerControls = isVisible;
        OnControlsVisibilityChanged?.Invoke(isVisible);
    }

    /// Initializes toggle based on saved preferences and sets up listener
    private void Start()
    {
        controlsToggle.SetIsOnWithoutNotify(ShowMultiplayerControls);
        controlsToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    /// Handles state changes for the controls toggle and persists the setting
    private static void OnToggleChanged(bool isOn)
    {
        ShowMultiplayerControls = isOn;

        PlayerPrefs.SetInt(MultiplayerControlsKey, isOn ? 1 : 0);
        PlayerPrefs.Save();

        OnControlsVisibilityChanged?.Invoke(ShowMultiplayerControls);
    }

    /// Cleans up event listeners when component is destroyed
    private void OnDestroy()
    {
        if (controlsToggle != null)
            controlsToggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
