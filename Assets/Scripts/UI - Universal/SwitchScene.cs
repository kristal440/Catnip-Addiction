using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class SwitchScene : MonoBehaviour
{
    public static void LoadMainMenu()
    {
        PhotonNetwork.Disconnect();
        SceneManager.LoadScene("MainMenu");
    }

    public static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
