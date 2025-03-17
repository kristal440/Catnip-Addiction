using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomPanel : MonoBehaviour
{
    [Header("Panel Controls")]
    public Button hideButton;
    public GameObject panel;
    public Button showButton;

    [Header("Error popup")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TMP_InputField playerNameInputField;

    private void Start()
    {
        panel.SetActive(false);
        showButton.interactable = true;
        hideButton.interactable = false;

        showButton.onClick.AddListener(ShowPanel);
        hideButton.onClick.AddListener(HidePanel);
    }

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

    private void HidePanel()
    {
        panel.SetActive(false);
        showButton.interactable = true;
        hideButton.interactable = false;
    }
}
