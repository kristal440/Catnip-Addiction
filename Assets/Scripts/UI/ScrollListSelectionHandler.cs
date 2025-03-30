using UnityEngine;
using UnityEngine.UI;

public class ScrollListSelectionHandler : MonoBehaviour
{
    // Define a delegate that provides more context
    public delegate void ItemSelectedHandler(int index, GameObject item);

    public event ItemSelectedHandler OnItemSelected;

    private ScrollListController _visualController;

    private void Awake()
    {
        _visualController = GetComponent<ScrollListController>();
        if (_visualController != null)
            _visualController.OnSelectionChanged += HandleVisualSelectionChanged;
    }

    private void OnDestroy()
    {
        if (_visualController != null)
            _visualController.OnSelectionChanged -= HandleVisualSelectionChanged;
    }

    internal void Initialize()
    {
        SetupButtons();
    }

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

    private void HandleItemClick(int index)
    {
        _visualController.SelectItem(index);
    }

    private void HandleVisualSelectionChanged(int index)
    {
        var itemGameObject = _visualController.GetItemAt(index) != null ? _visualController.GetItemAt(index).gameObject : null;

        if (itemGameObject != null)
            OnItemSelected?.Invoke(index, itemGameObject);
    }

    public int GetSelectedIndex()
    {
        return _visualController.CurrentIndex;
    }

    public GameObject GetSelectedItem()
    {
        var index = _visualController.CurrentIndex;
        return _visualController.GetItemAt(index) != null ? _visualController.GetItemAt(index).gameObject : null;
    }

    internal void SelectItemProgrammatically(int index)
    {
        _visualController.SelectItem(index);
    }
}
