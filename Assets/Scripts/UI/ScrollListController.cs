using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class ScrollListController : MonoBehaviour
{
    [Header("References")]
    public RectTransform viewport;
    public ScrollRect scrollRect;
    public GameObject mapEntryPrefab;

    [Header("Scaling Settings")]
    public float selectedScale = 1.1f;
    public float unselectedScale = 0.82f;
    public float scaleSmoothing = 6.6f;
    [Range(0, 1)] public float selectedAlpha = 1.0f;
    [Range(0, 1)] public float unselectedAlpha = 0.78f;
    public float alphaSmoothing = 100f;

    [Header("Snapping Settings")]
    public float snapSpeed = 15f;
    [SerializeField] private float baseItemSpacing;

    [Header("Height Expansion")]
    public float heightExpansionAmount = 40f;
    public float childHeightExpansionAmount = 45f;
    public float expansionSpeed = 20f;

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

    private void Start()
    {
        if (!scrollRect) scrollRect = GetComponentInParent<ScrollRect>();
        if (!viewport && scrollRect) viewport = scrollRect.viewport;
        _verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

        InitializeReferenceDimensions();
        InitializeItems();
        UpdatePadding();
    }

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

    internal void SelectItem(int index)
    {
        if (index < 0 || index >= itemRects.Count || index == CurrentIndex) return;

        CurrentIndex = index;
        _needsContentUpdate = true;
        OnSelectionChanged?.Invoke(CurrentIndex);
        UpdatePadding();

        StartCoroutine(ScrollToSelectedItem());
    }

    private System.Collections.IEnumerator ScrollToSelectedItem()
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

    private void Update()
    {
        HandleSnapping();
        UpdateItemScalesAndHeights();

        if (!_needsContentUpdate) return;

        UpdatePadding();
        _needsContentUpdate = false;
    }

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

    private void OnRectTransformDimensionsChange()
    {
        UpdatePadding();
    }

    internal List<RectTransform> GetItemRects()
    {
        return itemRects;
    }

    internal RectTransform GetItemAt(int index)
    {
        return index >= 0 && index < itemRects.Count ? itemRects[index] : null;
    }
}
