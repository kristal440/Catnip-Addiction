using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the visual behavior of a scrollable list with scaling, snapping, and expansion effects.
/// </summary>
/// <inheritdoc />
[RequireComponent(typeof(VerticalLayoutGroup))]
public class ScrollListController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] [Tooltip("The viewport containing the scrollable content")] public RectTransform viewport;
    [SerializeField] [Tooltip("The ScrollRect component controlling the scrolling behavior")] public ScrollRect scrollRect;
    [SerializeField] [Tooltip("Prefab used as template for list entries")] public GameObject mapEntryPrefab;

    [Header("Scaling Settings")]
    [SerializeField] [Tooltip("Scale factor for selected items")] public float selectedScale = 1.1f;
    [SerializeField] [Tooltip("Scale factor for unselected items")] public float unselectedScale = 0.82f;
    [SerializeField] [Tooltip("How quickly items scale when selection changes")] public float scaleSmoothing = 6.6f;
    [SerializeField] [Range(0, 1)] [Tooltip("Alpha value for selected items")] public float selectedAlpha = 1.0f;
    [SerializeField] [Range(0, 1)] [Tooltip("Alpha value for unselected items")] public float unselectedAlpha = 0.78f;
    [SerializeField] [Tooltip("How quickly alpha changes when selection changes")] public float alphaSmoothing = 100f;

    [Header("Snapping Settings")]
    [SerializeField] [Tooltip("How quickly the list snaps to selected items")] public float snapSpeed = 15f;
    [SerializeField] [Tooltip("Base spacing between list items")] private float baseItemSpacing;

    [Header("Height Expansion")]
    [SerializeField] [Tooltip("How much to expand item height when selected")] public float heightExpansionAmount = 40f;
    [SerializeField] [Tooltip("How much to expand child visual height when selected")] public float childHeightExpansionAmount = 45f;
    [SerializeField] [Tooltip("Speed of height expansion/contraction")] public float expansionSpeed = 20f;
    [SerializeField] private List<RectTransform> itemRects = new();
    [SerializeField] private List<float> originalHeights = new();
    [SerializeField] private List<float> originalChildHeights = new();

    private bool _isSnapping;
    private float _snapTargetPosition;
    private bool _needsContentUpdate;
    private VerticalLayoutGroup _verticalLayoutGroup;

    private float _referenceHeight;
    private float _referenceChildHeight;
    private float _referenceWidth;
    private float _referenceExpandedHeight;

    private readonly Vector3[] _corners = new Vector3[4];

    private float ItemSpacing => Mathf.Max(2f, baseItemSpacing / Mathf.Max(1f, Mathf.Sqrt(itemRects.Count)));

    public event Action<int> OnSelectionChanged;
    internal int CurrentIndex { get; private set; } = -1;

    // Initializes components and configures the list on startup
    private void Start()
    {
        if (!scrollRect) scrollRect = GetComponentInParent<ScrollRect>();
        if (!viewport && scrollRect) viewport = scrollRect.viewport;
        _verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

        InitializeReferenceDimensions();
        InitializeItems();
        UpdatePadding();
    }

    // Sets up reference dimensions based on the prefab
    private void InitializeReferenceDimensions()
    {
        if (!mapEntryPrefab) return;

        var prefabRect = mapEntryPrefab.GetComponent<RectTransform>();
        if (!prefabRect) return;

        var rect = prefabRect.rect;
        _referenceHeight = rect.height;
        _referenceWidth = rect.width;

        var visualChild = prefabRect.Find("visual") != null ? prefabRect.Find("visual").GetComponent<RectTransform>() : null;

        _referenceChildHeight = visualChild ? visualChild.rect.height : _referenceHeight;

        if (heightExpansionAmount == 0)
            heightExpansionAmount = _referenceHeight * 0.2f;

        if (childHeightExpansionAmount == 0)
            childHeightExpansionAmount = _referenceChildHeight * 0.25f;

        if (baseItemSpacing == 0)
            baseItemSpacing = _referenceHeight * 0.15f;

        _referenceExpandedHeight = _referenceHeight + heightExpansionAmount;
    }

    // Collects all item RectTransforms and stores their original dimensions
    internal void InitializeItems()
    {
        itemRects.Clear();
        originalHeights.Clear();
        originalChildHeights.Clear();

        if (!_verticalLayoutGroup)
            _verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

        for (var i = 0; i < transform.childCount; i++)
        {
            var itemRect = transform.GetChild(i).GetComponent<RectTransform>();
            if (!itemRect) continue;

            itemRects.Add(itemRect);

            var itemHeight = itemRect.sizeDelta.y;
            if (itemHeight <= 0 && _referenceHeight > 0)
            {
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, _referenceHeight);
                itemHeight = _referenceHeight;
            }
            originalHeights.Add(itemHeight);

            var visualChild = itemRect.Find("visual") != null ? itemRect.Find("visual").GetComponent<RectTransform>() : null;

            var childHeight = visualChild ? visualChild.sizeDelta.y : 0f;

            if (childHeight <= 0 && _referenceChildHeight > 0 && visualChild)
            {
                visualChild.sizeDelta = new Vector2(visualChild.sizeDelta.x, _referenceChildHeight);
                childHeight = _referenceChildHeight;
            }
            originalChildHeights.Add(childHeight);
        }

        if (_verticalLayoutGroup)
            _verticalLayoutGroup.spacing = ItemSpacing;
    }

    // Updates vertical padding to center items in the viewport
    private void UpdatePadding()
    {
        if (!viewport || !_verticalLayoutGroup) return;

        var viewportHeight = viewport.rect.height;
        var itemHeight = CurrentIndex >= 0 ? _referenceExpandedHeight : _referenceHeight;
        var paddingValue = Mathf.Max(0, (viewportHeight / 2f) - (itemHeight / 2f));

        var newPadding = Mathf.RoundToInt(paddingValue);
        if (newPadding == _verticalLayoutGroup.padding.top) return;

        _verticalLayoutGroup.padding = new RectOffset(
            _verticalLayoutGroup.padding.left,
            _verticalLayoutGroup.padding.right,
            newPadding,
            newPadding
        );

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    // Selects an item by index and triggers visual updates
    internal void SelectItem(int index)
    {
        if (index < 0 || index >= itemRects.Count || index == CurrentIndex) return;

        CurrentIndex = index;
        _needsContentUpdate = true;
        OnSelectionChanged?.Invoke(CurrentIndex);
        UpdatePadding();

        StartCoroutine(ScrollToSelectedItem());
    }

    // Smoothly scrolls to center the selected item in the viewport
    private IEnumerator ScrollToSelectedItem()
    {
        yield return null;

        if (CurrentIndex < 0 || CurrentIndex >= itemRects.Count) yield break;

        Canvas.ForceUpdateCanvases();

        var itemRect = itemRects[CurrentIndex];
        var contentHeight = ((RectTransform)transform).rect.height;
        var viewportHeight = viewport.rect.height;

        var itemTop = -itemRect.anchoredPosition.y - _verticalLayoutGroup.padding.top;
        var itemCenter = itemTop - (itemRect.rect.height / 2f);

        var scrollPosition = itemCenter / (contentHeight - viewportHeight);
        _snapTargetPosition = Mathf.Clamp01(1f - scrollPosition);
        _isSnapping = true;
    }

    // Updates snapping and item visual states each frame
    private void Update()
    {
        HandleSnapping();
        UpdateItemScalesAndHeights();

        if (!_needsContentUpdate) return;

        UpdatePadding();
        _needsContentUpdate = false;
    }

    // Handles smooth snapping to the selected item
    private void HandleSnapping()
    {
        if (!_isSnapping) return;

        scrollRect.verticalNormalizedPosition = Mathf.Lerp(
            scrollRect.verticalNormalizedPosition,
            _snapTargetPosition,
            snapSpeed * Time.deltaTime
        );

        if (!(Mathf.Abs(scrollRect.verticalNormalizedPosition - _snapTargetPosition) < 0.001f)) return;

        scrollRect.verticalNormalizedPosition = _snapTargetPosition;
        _isSnapping = false;
    }

    // Updates the scale, alpha, and height of all list items based on viewport position
    private void UpdateItemScalesAndHeights()
    {
        if (itemRects.Count == 0 || !viewport) return;

        viewport.GetWorldCorners(_corners);
        var viewportTop = _corners[1].y;
        var viewportBottom = _corners[0].y;
        var viewportHeight = viewportTop - viewportBottom;
        var viewportCenter = (viewportTop + viewportBottom) * 0.5f;

        var heightChanged = false;
        var deltaTime = Time.deltaTime;

        for (var i = 0; i < itemRects.Count; i++)
        {
            var item = itemRects[i];
            item.GetWorldCorners(_corners);
            var itemCenterY = (_corners[0].y + _corners[1].y) * 0.5f;

            var distanceFromCenter = Mathf.Clamp01(Mathf.Abs(itemCenterY - viewportCenter) / (viewportHeight * 0.5f));
            var targetScale = Mathf.Lerp(selectedScale, unselectedScale, distanceFromCenter);

            item.localScale = Vector3.Lerp(
                item.localScale,
                new Vector3(targetScale, targetScale, 1f),
                scaleSmoothing * deltaTime
            );

            var visualChild = item.Find("visual") ? item.Find("visual").GetComponent<RectTransform>() : null;

            if (visualChild)
            {
                heightChanged |= UpdateVisualChildAppearance(visualChild, i, deltaTime);
                heightChanged |= UpdateVisualChildHeight(visualChild, i, deltaTime);
            }

            heightChanged |= UpdateItemHeight(item, i, deltaTime);
        }

        if (heightChanged)
            _needsContentUpdate = true;
    }

    // Updates the alpha value of a list item's visual element
    private bool UpdateVisualChildAppearance(Component visualChild, int index, float deltaTime)
    {
        var image = visualChild.GetComponent<Image>();
        if (!image) return false;

        var targetAlpha = index == CurrentIndex ? selectedAlpha : unselectedAlpha;
        var color = image.color;
        var newAlpha = Mathf.Lerp(color.a, targetAlpha, alphaSmoothing * deltaTime);

        if (Mathf.Abs(newAlpha - color.a) <= 0.001f) return false;

        image.color = new Color(color.r, color.g, color.b, newAlpha);
        return true;
    }

    // Updates the height of a list item's visual child element
    private bool UpdateVisualChildHeight(RectTransform visualChild, int index, float deltaTime)
    {
        var childTargetHeight = _referenceChildHeight;
        if (index == CurrentIndex)
            childTargetHeight += childHeightExpansionAmount;

        var childCurrentSize = visualChild.sizeDelta;
        var childNewHeight = Mathf.Lerp(childCurrentSize.y, childTargetHeight, expansionSpeed * deltaTime);

        if (Mathf.Abs(childNewHeight - childCurrentSize.y) <= 0.01f) return false;

        visualChild.sizeDelta = new Vector2(_referenceWidth, childNewHeight);
        return true;
    }

    // Updates the height of a list item
    private bool UpdateItemHeight(RectTransform item, int index, float deltaTime)
    {
        var targetHeight = _referenceHeight;
        if (index == CurrentIndex)
            targetHeight += heightExpansionAmount;

        var currentSize = item.sizeDelta;
        var newHeight = Mathf.Lerp(currentSize.y, targetHeight, expansionSpeed * deltaTime);

        if (Mathf.Abs(newHeight - currentSize.y) <= 0.01f) return false;

        item.sizeDelta = new Vector2(_referenceWidth, newHeight);
        return true;
    }

    // Updates padding when the transform dimensions change
    private void OnRectTransformDimensionsChange()
    {
        UpdatePadding();
    }

    // Returns all item RectTransforms in the list
    internal List<RectTransform> GetItemRects()
    {
        return itemRects;
    }

    // Returns the RectTransform at the specified index
    internal RectTransform GetItemAt(int index)
    {
        return index >= 0 && index < itemRects.Count ? itemRects[index] : null;
    }
}
