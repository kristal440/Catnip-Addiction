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

    private void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        var itemRects = _visualController.GetItemRects();

        for (var i = 0; i < itemRects.Count; i++)
        {
            var button = itemRects[i].GetComponent<Button>() ??
                         itemRects[i].gameObject.AddComponent<Button>();

            if (button.colors.normalColor.a == 0)
            {
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                button.colors = colors;
            }

            var index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleItemClick(index));
        }
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
