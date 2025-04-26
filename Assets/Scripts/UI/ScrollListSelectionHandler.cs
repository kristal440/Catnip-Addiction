using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles selection and click events for items in a scrollable list.
/// </summary>
/// <inheritdoc />
public class ScrollListSelectionHandler : MonoBehaviour
{
    public delegate void ItemSelectedHandler(int index, GameObject item);
    public event ItemSelectedHandler OnItemSelected;
    private ScrollListController _visualController;

    // Sets up connections to the visual controller
    private void Awake()
    {
        _visualController = GetComponent<ScrollListController>();
        if (_visualController != null)
            _visualController.OnSelectionChanged += HandleVisualSelectionChanged;
    }

    // Cleans up event connections when destroyed
    private void OnDestroy()
    {
        if (_visualController != null)
            _visualController.OnSelectionChanged -= HandleVisualSelectionChanged;
    }

    // Initializes the selection handler
    internal void Initialize()
    {
        SetupButtons();
    }

    // Configures click handlers for all buttons in the list
    private void SetupButtons()
    {
        var itemRects = _visualController.GetItemRects();

        for (var i = 0; i < itemRects.Count; i++)
        {
            var button = itemRects[i].GetComponent<Button>();
            if (button == null) continue;

            var index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleItemClick(index));
        }
    }

    // Forwards the click event to the visual controller
    private void HandleItemClick(int index)
    {
        _visualController.SelectItem(index);
    }

    // Broadcasts selection changes via the OnItemSelected event
    private void HandleVisualSelectionChanged(int index)
    {
        var itemGameObject = _visualController.GetItemAt(index) != null ? _visualController.GetItemAt(index).gameObject : null;

        if (itemGameObject != null)
            OnItemSelected?.Invoke(index, itemGameObject);
    }

    // Returns the currently selected item index
    public int GetSelectedIndex()
    {
        return _visualController.CurrentIndex;
    }

    // Returns the currently selected item GameObject
    public GameObject GetSelectedItem()
    {
        var index = _visualController.CurrentIndex;
        return _visualController.GetItemAt(index) != null ? _visualController.GetItemAt(index).gameObject : null;
    }

    // Programmatically selects an item by index
    internal void SelectItemProgrammatically(int index)
    {
        _visualController.SelectItem(index);
    }
}
