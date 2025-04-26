using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Handles the UI panel used for creating new multiplayer game rooms.
/// </summary>
public class CreateRoomPanel : MonoBehaviour
{
    [Header("Panel Controls")]
    [SerializeField] [Tooltip("Button that hides the create room panel")] public Button hideButton;
    [SerializeField] [Tooltip("The panel container that holds room creation UI elements")] public GameObject panel;
    [SerializeField] [Tooltip("Button that shows the create room panel")] public Button showButton;

    [Header("Error popup")]
    [SerializeField] [Tooltip("Panel displayed when errors occur during room creation")] private GameObject errorPanel;
    [SerializeField] [Tooltip("Text component that displays error messages")] private TextMeshProUGUI errorText;
    [SerializeField] [Tooltip("Input field for entering the player name")] private TMP_InputField playerNameInputField;

    /// Initializes panel state and registers button listeners
    private void Start()
    {
        panel.SetActive(false);
        showButton.interactable = true;
        hideButton.interactable = false;

        showButton.onClick.AddListener(ShowPanel);
        hideButton.onClick.AddListener(HidePanel);
    }

    /// Shows the panel after validating player name requirements
    private void ShowPanel()
    {
        if (playerNameInputField.text.Length < 3)
        {
            errorText.text = "Player name must be at least 3 characters long!";
            errorPanel.SetActive(true);
            return;
        }
        panel.SetActive(true);
        showButton.interactable = false;
        hideButton.interactable = true;
    }

    /// Hides the panel and updates button states
    private void HidePanel()
    {
        panel.SetActive(false);
        showButton.interactable = true;
        hideButton.interactable = false;
    }
}
