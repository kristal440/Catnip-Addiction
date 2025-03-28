using UnityEngine;

public class GameStartup : MonoBehaviour
{
    [SerializeField] private int defaultTargetFPS;

    private const string VsyncEnabledKey = "VSyncEnabled";
    private const string TargetFPSKey = "TargetFPS";
    private const string MultiplayerControlsKey = "ShowMultiplayerControls";

    private void Start()
    {
        ApplyFPSSettings();
        ApplyOnScreenControlsSettings();

        Application.runInBackground = true;
        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
    }

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
