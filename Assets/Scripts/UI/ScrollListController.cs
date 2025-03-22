using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class ScrollListController : MonoBehaviour
{
    [Header("References")]
    public RectTransform viewport;
    public ScrollRect scrollRect;

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

    private int _currentIndex = -1;
    private readonly List<RectTransform> _itemRects = new();
    private readonly List<float> _originalHeights = new();
    private readonly List<float> _originalChildHeights = new();
    private bool _isSnapping;
    private float _snapTargetPosition;
    private bool _needsContentUpdate;
    private VerticalLayoutGroup _verticalLayoutGroup;

    private readonly Vector3[] _tempViewportCorners = new Vector3[4];
    private readonly Vector3[] _tempItemCorners = new Vector3[4];

    private float ItemSpacing => Mathf.Max(2f, baseItemSpacing / Mathf.Max(1f, Mathf.Sqrt(_itemRects.Count)));

    public int CurrentIndex => _currentIndex;

    private void Start()
    {
        scrollRect ??= GetComponentInParent<ScrollRect>();
        if (scrollRect && !viewport) viewport = scrollRect.viewport;
        _verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

        InitializeItems();
        UpdatePadding();
    }

    private void InitializeItems()
    {
        _itemRects.Clear();
        _originalHeights.Clear();
        _originalChildHeights.Clear();

        for (var i = 0; i < transform.childCount; i++)
        {
            var itemRect = transform.GetChild(i).GetComponent<RectTransform>();
            if (!itemRect) continue;

            _itemRects.Add(itemRect);
            _originalHeights.Add(itemRect.sizeDelta.y);

            var visualChild = itemRect.Find("visual")?.GetComponent<RectTransform>();
            _originalChildHeights.Add(visualChild != null ? visualChild.sizeDelta.y : 0f);
        }

        _verticalLayoutGroup.spacing = ItemSpacing;
    }

    private void UpdatePadding()
    {
        if (!viewport || !_verticalLayoutGroup) return;

        var viewportHeight = viewport.rect.height;
        var halfViewport = viewportHeight / 2f;
        var paddingValue = halfViewport;

        if (_currentIndex >= 0 && _currentIndex < _itemRects.Count)
        {
            var selectedItem = _itemRects[_currentIndex];
            paddingValue = halfViewport - selectedItem.rect.height / 2f;
        }

        paddingValue = Mathf.Max(0, paddingValue);

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

    public void SelectItem(int index)
    {
        if (index < 0 || index >= _itemRects.Count || index == _currentIndex) return;

        _currentIndex = index;
        _needsContentUpdate = true;

        UpdatePadding();
        StartCoroutine(ScrollToSelectedItem());
    }

    private System.Collections.IEnumerator ScrollToSelectedItem()
    {
        yield return null;

        if (_currentIndex < 0 || _currentIndex >= _itemRects.Count) yield break;

        Canvas.ForceUpdateCanvases();

        var itemRect = _itemRects[_currentIndex];
        var contentHeight = ((RectTransform)transform).rect.height;
        var viewportHeight = viewport.rect.height;

        var itemTop = -itemRect.anchoredPosition.y - _verticalLayoutGroup.padding.top;
        var itemBottom = itemTop - itemRect.rect.height;
        var itemCenter = (itemTop + itemBottom) * 0.5f;

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
        if (_itemRects.Count == 0 || !viewport) return;

        viewport.GetWorldCorners(_tempViewportCorners);
        var viewportTop = _tempViewportCorners[1].y;
        var viewportBottom = _tempViewportCorners[0].y;
        var viewportHeight = viewportTop - viewportBottom;
        var viewportCenter = (viewportTop + viewportBottom) * 0.5f;

        var heightChanged = false;

        for (var i = 0; i < _itemRects.Count; i++)
        {
            var item = _itemRects[i];
            item.GetWorldCorners(_tempItemCorners);
            var itemCenterY = (_tempItemCorners[0].y + _tempItemCorners[1].y) * 0.5f;

            var distanceFromCenter = Mathf.Clamp01(Mathf.Abs(itemCenterY - viewportCenter) / (viewportHeight * 0.5f));

            var targetScale = Mathf.Lerp(selectedScale, unselectedScale, distanceFromCenter);
            item.localScale = Vector3.Lerp(
                item.localScale,
                new Vector3(targetScale, targetScale, 1f),
                scaleSmoothing * Time.deltaTime
            );

            var visualChild = item.Find("visual")?.GetComponent<RectTransform>();
            if (visualChild)
            {
                UpdateVisualChildAppearance(visualChild, i);
                heightChanged |= UpdateVisualChildHeight(visualChild, i);
            }

            heightChanged |= UpdateItemHeight(item, i);
        }

        if (heightChanged)
            _needsContentUpdate = true;
    }

    private void UpdateVisualChildAppearance(RectTransform visualChild, int index)
    {
        var image = visualChild.GetComponent<Image>();
        if (!image) return;

        var targetAlpha = index == _currentIndex ? selectedAlpha : unselectedAlpha;
        var color = image.color;
        var newAlpha = Mathf.Lerp(color.a, targetAlpha, alphaSmoothing * Time.deltaTime);

        if (Mathf.Abs(newAlpha - color.a) <= 0.001f) return;

        image.color = new Color(color.r, color.g, color.b, newAlpha);
    }

    private bool UpdateVisualChildHeight(RectTransform visualChild, int index)
    {
        var childTargetHeight = _originalChildHeights[index];
        if (index == _currentIndex)
            childTargetHeight += childHeightExpansionAmount;

        var childCurrentSize = visualChild.sizeDelta;
        var childNewHeight = Mathf.Lerp(childCurrentSize.y, childTargetHeight, expansionSpeed * Time.deltaTime);

        if (Mathf.Abs(childNewHeight - childCurrentSize.y) <= 0.01f) return false;

        visualChild.sizeDelta = new Vector2(childCurrentSize.x, childNewHeight);
        return true;
    }

    private bool UpdateItemHeight(RectTransform item, int index)
    {
        var targetHeight = _originalHeights[index] + (index == _currentIndex ? heightExpansionAmount : 0);
        var currentSize = item.sizeDelta;
        var newHeight = Mathf.Lerp(currentSize.y, targetHeight, expansionSpeed * Time.deltaTime);

        if (Mathf.Abs(newHeight - currentSize.y) <= 0.01f) return false;

        item.sizeDelta = new Vector2(currentSize.x, newHeight);
        return true;
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdatePadding();
    }

    public List<RectTransform> GetItemRects()
    {
        return _itemRects;
    }
}
