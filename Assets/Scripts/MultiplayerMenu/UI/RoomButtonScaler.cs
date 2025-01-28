using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RoomButtonScaler : MonoBehaviour, IScrollHandler, IBeginDragHandler, IEndDragHandler
{
    public RectTransform viewport;
    public float minScale = 0.7f;
    public float normalScale = 1f;

    private VerticalLayoutGroup _layoutGroup;

    private void Start()
    {
        _layoutGroup = GetComponent<VerticalLayoutGroup>();

        if (viewport != null) return;
        Debug.LogError("Viewport not assigned in RoomCardScaler!");
    }

    private void Update()
    {
        UpdateCardScales();
    }

    private void UpdateCardScales()
    {
        if (!_layoutGroup || !viewport) return;

        var viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);

        foreach (Transform child in transform)
        {
            var childRect = child.GetComponent<RectTransform>();
            if (!childRect) continue;
            var childCenterWorld = childRect.TransformPoint(childRect.rect.center);
            var distance = Mathf.Abs(childCenterWorld.y - viewportCenterWorld.y);
            var normalizedDistance = Mathf.Clamp01(distance / (viewport.rect.height / 2f));
            var scaleFactor = Mathf.Lerp(normalScale, minScale, normalizedDistance);

            // Apply the horizontal scale
            childRect.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        UpdateCardScales();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Optional
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Optional
    }
}
