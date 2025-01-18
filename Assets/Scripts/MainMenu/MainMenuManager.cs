using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Button singleplayerButton;
    public Button multiplayerButton;

    void Start()
    {
        singleplayerButton.onClick.AddListener(LoadSingleplayer);
        multiplayerButton.onClick.AddListener(LoadMultiplayerMenu);
    }

    public void LoadSingleplayer()
    {
        // For now, directly load the game scene. We'll add singleplayer logic later.
        SceneManager.LoadScene("GameScene");
    }

    public void LoadMultiplayerMenu()
    {
        SceneManager.LoadScene("MultiplayerMenu");
    }
}
