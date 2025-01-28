using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    public Button showButton;
    public Button hideButton;
    public GameObject panel;

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
