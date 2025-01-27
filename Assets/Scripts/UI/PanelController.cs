using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    public Button showButton; // Assign the "Show" button in the Inspector
    public Button hideButton; // Assign the "Hide" button in the Inspector
    public GameObject panel;  // Assign the panel GameObject in the Inspector

    private void Start()
    {
        // Ensure the initial state is correct
        panel.SetActive(false); // Hide the panel initially
        showButton.interactable = true; // Enable the "Show" button
        hideButton.interactable = false; // Disable the "Hide" button

        // Add listeners to the buttons
        showButton.onClick.AddListener(ShowPanel);
        hideButton.onClick.AddListener(HidePanel);
    }

    private void ShowPanel()
    {
        panel.SetActive(true); // Show the panel
        showButton.interactable = false; // Disable the "Show" button
        hideButton.interactable = true; // Enable the "Hide" button
    }

    private void HidePanel()
    {
        panel.SetActive(false); // Hide the panel
        showButton.interactable = true; // Enable the "Show" button
        hideButton.interactable = false; // Disable the "Hide" button
    }
}