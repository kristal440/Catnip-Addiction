using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class RoomButtonScaler : MonoBehaviour
{
    [Header("References")]
    public RectTransform viewport;
    public ScrollRect scrollRect;

    [Header("Scaling Settings")]
    public float selectedScale = 1.0f;
    public float unselectedScale = 0.7f;
    public float scaleSmoothing = 3f;
    [Range(0, 1)] public float selectedAlpha = 1.0f;
    [Range(0, 1)] public float unselectedAlpha = 0.5f;
    public float alphaSmoothing = 3f;

    [Header("Snapping Settings")]
    public float snapSpeed = 5f;
    [SerializeField] private float baseItemSpacing = 10f;

    private float ItemSpacing => Mathf.Max(2f, baseItemSpacing / Mathf.Max(1f, Mathf.Sqrt(_itemRects.Count)));

    [Header("Height Expansion")]
    public float heightExpansionAmount = 20f;
    public float childHeightExpansionAmount = 10f;
    public float expansionSpeed = 3f;

    private int _currentIndex = -1;
    private readonly List<RectTransform> _itemRects = new();
    private readonly List<float> _originalHeights = new();
    private readonly List<float> _originalChildHeights = new();
    private float _contentStartPosition;
    private bool _isSnapping;
    private float _snapTargetPosition;
    private bool _needsContentUpdate;
    private VerticalLayoutGroup _verticalLayoutGroup;

    private void Start()
    {
        if (!scrollRect)
            scrollRect = GetComponentInParent<ScrollRect>();

        if (!viewport && scrollRect)
            viewport = scrollRect.viewport;

        _verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();
        if (_verticalLayoutGroup == null)
            _verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();

        InitializeItems();
        SetupButtons();
        UpdatePadding();
    }

    private void OnDestroy()
    {
        foreach (var t in _itemRects)
        {
            var button = t?.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }

    private void InitializeItems()
    {
        _itemRects.Clear();
        _originalHeights.Clear();
        _originalChildHeights.Clear();

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var itemRect = child.GetComponent<RectTransform>();

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
            var halfItemHeight = selectedItem.rect.height / 2f;
            paddingValue = halfViewport - halfItemHeight;
        }

        paddingValue = Mathf.Max(0, paddingValue);

        var padding = new RectOffset(
            _verticalLayoutGroup.padding.left,
            _verticalLayoutGroup.padding.right,
            Mathf.RoundToInt(paddingValue),
            Mathf.RoundToInt(paddingValue)
        );

        _verticalLayoutGroup.padding = padding;
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private void SetupButtons()
    {
        for (var i = 0; i < _itemRects.Count; i++)
        {
            var button = _itemRects[i].GetComponent<Button>();
            if (button == null)
            {
                button = _itemRects[i].gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                button.colors = colors;
            }

            var index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectItem(index));
        }
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

    private void SelectItem(int index)
    {
        if (index < 0 || index >= _itemRects.Count || index == _currentIndex) return;

        _currentIndex = index;
        _needsContentUpdate = true;

        UpdatePadding();
        StartCoroutine(ScrollToSelectedItem());
    }

    private void Update()
    {
        if (_isSnapping)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(
                scrollRect.verticalNormalizedPosition,
                _snapTargetPosition,
                snapSpeed * Time.deltaTime
            );

            if (Mathf.Abs(scrollRect.verticalNormalizedPosition - _snapTargetPosition) < 0.001f)
            {
                scrollRect.verticalNormalizedPosition = _snapTargetPosition;
                _isSnapping = false;
            }
        }

        UpdateItemScalesAndHeights();

        if (!_needsContentUpdate) return;
        UpdatePadding();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        _needsContentUpdate = false;
    }

    private void UpdateItemScalesAndHeights()
    {
        if (_itemRects.Count == 0 || !viewport) return;

        var viewportCorners = new Vector3[4];
        viewport.GetWorldCorners(viewportCorners);

        var viewportTop = viewportCorners[1].y;
        var viewportBottom = viewportCorners[0].y;
        var viewportHeight = viewportTop - viewportBottom;
        var viewportCenter = (viewportTop + viewportBottom) * 0.5f;

        var heightChanged = false;

        for (var i = 0; i < _itemRects.Count; i++)
        {
            var itemCorners = new Vector3[4];
            _itemRects[i].GetWorldCorners(itemCorners);

            var itemCenterY = (itemCorners[0].y + itemCorners[1].y) * 0.5f;

            var distanceFromCenter = Mathf.Abs(itemCenterY - viewportCenter) / (viewportHeight * 0.5f);
            distanceFromCenter = Mathf.Clamp01(distanceFromCenter);

            var targetScale = Mathf.Lerp(selectedScale, unselectedScale, distanceFromCenter);
            var targetAlpha = i == _currentIndex ? selectedAlpha : unselectedAlpha;

            _itemRects[i].localScale = Vector3.Lerp(
                _itemRects[i].localScale,
                new Vector3(targetScale, targetScale, 1f),
                scaleSmoothing * Time.deltaTime
            );

            var visualChild = _itemRects[i].Find("visual")?.GetComponent<RectTransform>();
            if (visualChild)
            {
                var image = visualChild.GetComponent<Image>();
                if (image)
                {
                    var color = image.color;
                    var currentAlpha = color.a;
                    var newAlpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaSmoothing * Time.deltaTime);

                    if (Mathf.Abs(newAlpha - currentAlpha) > 0.001f)
                    {
                        image.color = new Color(color.r, color.g, color.b, newAlpha);
                    }
                }

                var childTargetHeight = _originalChildHeights[i];
                if (i == _currentIndex)
                    childTargetHeight += childHeightExpansionAmount;

                var childCurrentSize = visualChild.sizeDelta;
                var childNewHeight = Mathf.Lerp(childCurrentSize.y, childTargetHeight, expansionSpeed * Time.deltaTime);

                if (Mathf.Abs(childNewHeight - childCurrentSize.y) > 0.01f)
                {
                    visualChild.sizeDelta = new Vector2(childCurrentSize.x, childNewHeight);
                    heightChanged = true;
                }
            }

            var targetHeight = _originalHeights[i];
            if (i == _currentIndex)
                targetHeight += heightExpansionAmount;

            var currentSize = _itemRects[i].sizeDelta;
            var newHeight = Mathf.Lerp(currentSize.y, targetHeight, expansionSpeed * Time.deltaTime);

            if (!(Mathf.Abs(newHeight - currentSize.y) > 0.01f)) continue;
            _itemRects[i].sizeDelta = new Vector2(currentSize.x, newHeight);
            heightChanged = true;
        }

        if (heightChanged)
            _needsContentUpdate = true;
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdatePadding();
    }
}
