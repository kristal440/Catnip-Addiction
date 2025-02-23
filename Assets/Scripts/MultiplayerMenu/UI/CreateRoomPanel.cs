using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomPanel : MonoBehaviour
{
    public Button showButton;
    public Button hideButton;
    public GameObject panel;

    [Header("Error popup")]
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

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