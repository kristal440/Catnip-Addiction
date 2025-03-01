using UnityEngine;

public class PanelCloser : MonoBehaviour
{
    public GameObject panel;

    public void ClosePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
}