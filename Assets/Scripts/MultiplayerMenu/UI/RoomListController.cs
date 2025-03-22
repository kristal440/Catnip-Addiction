using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoomListController : MonoBehaviour, IScrollHandler
{
    public float minScale = 0.7f;
    public float normalScale = 1f;
    public RectTransform viewport;

    private VerticalLayoutGroup _layoutGroup;

    private void Start()
    {
        _layoutGroup = GetComponent<VerticalLayoutGroup>();
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

            childRect.localScale = new Vector2(scaleFactor, scaleFactor);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        UpdateCardScales();
    }
}
