using UnityEngine;

/// <summary>
/// Manages UI panel visibility with options to instantiate or toggle existing panels
/// </summary>
/// <inheritdoc />
public class PanelManager : MonoBehaviour
{
    [SerializeField] [Tooltip("Panel reference to show/hide or instantiate")] private GameObject panel;
    private GameObject _instantiatedPanel;

    // Closes the currently active panel by destroying instance or deactivating
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

    // Opens the panel by activating an existing instance or creating a new one
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
