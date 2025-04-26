using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Initializes game settings like frame rate, vsync, and on-screen controls on startup.
/// </summary>
public class GameStartup : MonoBehaviour
{
    [SerializeField] [Tooltip("Default frame rate to use if not set in player preferences")] private int defaultTargetFPS;

    private const string VsyncEnabledKey = "VSyncEnabled";
    private const string TargetFPSKey = "TargetFPS";
    private const string MultiplayerControlsKey = "ShowMultiplayerControls";

    /// Initializes application settings and applies user preferences
    private void Start()
    {
        ApplyFPSSettings();
        ApplyOnScreenControlsSettings();

        Application.runInBackground = true;
        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
    }

    /// Configures vsync and frame rate based on saved preferences or platform defaults
    private void ApplyFPSSettings()
    {
        bool vsyncEnabled;
        if (PlayerPrefs.HasKey(VsyncEnabledKey))
        {
            vsyncEnabled = PlayerPrefs.GetInt(VsyncEnabledKey, 0) == 1;
        }
        else
        {
            var isMobilePlatform = Application.platform == RuntimePlatform.Android ||
                                   Application.platform == RuntimePlatform.IPhonePlayer;
            vsyncEnabled = isMobilePlatform;
            PlayerPrefs.SetInt(VsyncEnabledKey, vsyncEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;

        var targetFPS = PlayerPrefs.GetInt(TargetFPSKey, defaultTargetFPS);
        Application.targetFrameRate = targetFPS;
    }

    /// Configures on-screen controls visibility based on saved preferences or platform defaults
    private static void ApplyOnScreenControlsSettings()
    {
        bool showMultiplayerControls;
        if (PlayerPrefs.HasKey(MultiplayerControlsKey))
        {
            showMultiplayerControls = PlayerPrefs.GetInt(MultiplayerControlsKey) == 1;
        }
        else
        {
            showMultiplayerControls = Application.isMobilePlatform;
            PlayerPrefs.SetInt(MultiplayerControlsKey, showMultiplayerControls ? 1 : 0);
            PlayerPrefs.Save();
        }

        OnScreenControlsManager.ShowMultiplayerControls = showMultiplayerControls;
        OnScreenControlsManager.SetControlsVisibility(showMultiplayerControls);
    }
}
