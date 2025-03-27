using UnityEngine;
using UnityEngine.UI;
using System;

public class OnScreenControlsManager : MonoBehaviour
{
    [SerializeField] private Toggle controlsToggle;

    public static bool ShowMultiplayerControls { get; private set; }
    public static event Action<bool> OnControlsVisibilityChanged;

    private const string MultiplayerControlsKey = "ShowMultiplayerControls";

    private void Start()
    {
        if (PlayerPrefs.HasKey(MultiplayerControlsKey))
        {
            ShowMultiplayerControls = PlayerPrefs.GetInt(MultiplayerControlsKey) == 1;
        }
        else
        {
            ShowMultiplayerControls = Application.isMobilePlatform;
        }

        controlsToggle.SetIsOnWithoutNotify(ShowMultiplayerControls);
        OnControlsVisibilityChanged?.Invoke(ShowMultiplayerControls);
        controlsToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private static void OnToggleChanged(bool isOn)
    {
        ShowMultiplayerControls = isOn;

        PlayerPrefs.SetInt(MultiplayerControlsKey, isOn ? 1 : 0);
        PlayerPrefs.Save();

        OnControlsVisibilityChanged?.Invoke(ShowMultiplayerControls);
    }

    private void OnDestroy()
    {
        if (controlsToggle != null)
        {
            controlsToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}
