using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public GameObject panel;
    private GameObject _instantiatedPanel;

    public void ClosePanel()
    {
        if (_instantiatedPanel)
        {
            Destroy(_instantiatedPanel);
            _instantiatedPanel = null;
        }
        else if (panel)
        {
            panel.SetActive(false);
        }
    }

    public void OpenPanel()
    {
        if (!panel)
            return;

        if (_instantiatedPanel)
        {
            _instantiatedPanel.SetActive(true);
            return;
        }

        if (!panel.scene.IsValid())
        {
            var parent = transform.parent;

            _instantiatedPanel = Instantiate(panel, parent);

            _instantiatedPanel.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        }
        else
        {
            panel.SetActive(true);
        }
    }
}
