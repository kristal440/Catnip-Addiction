using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public GameObject panel;

    public void ClosePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }
    public void OpenPanel()
    {
        if (panel != null)
            panel.SetActive(true);
    }
}
