using UnityEngine;
using UnityEngine.UI;
using System;

public class ScrollListSelectionHandler : MonoBehaviour
{
    public event Action<int> OnItemSelected;

    private ScrollListController _visualController;

    private void Awake()
    {
        _visualController = GetComponent<ScrollListController>();
    }

    public void SetupButtons()
    {
        var itemRects = _visualController.GetItemRects();

        for (var i = 0; i < itemRects.Count; i++)
        {
            var button = itemRects[i].GetComponent<Button>();

            var index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleItemClick(index));
        }
        Debug.Log($"SetupButtons: {itemRects.Count} buttons set up in handler.");
    }

    private void HandleItemClick(int index)
    {
        _visualController.SelectItem(index);
        OnItemSelected?.Invoke(index);
    }

    protected int GetSelectedIndex()
    {
        return _visualController.CurrentIndex;
    }
}
