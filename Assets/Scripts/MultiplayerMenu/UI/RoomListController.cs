using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls a scrollable room list with dynamic scaling based on viewport position.
/// </summary>
/// <inheritdoc cref="MonoBehaviour" />
public class RoomListController : MonoBehaviour, IScrollHandler
{
    [SerializeField] [Tooltip("Minimum scale for room cards furthest from center")] public float minScale = 0.7f;
    [SerializeField] [Tooltip("Normal scale for room cards at the center")] public float normalScale = 1f;
    [SerializeField] [Tooltip("The viewport that contains the scrollable content")] public RectTransform viewport;

    private VerticalLayoutGroup _layoutGroup;

    // Initializes component references
    private void Start()
    {
        _layoutGroup = GetComponent<VerticalLayoutGroup>();
    }

    // Updates card scales every frame based on position
    private void Update()
    {
        UpdateCardScales();
    }

    // Adjusts the scale of room cards based on their distance from viewport center
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

    // Handles scroll events to update card scales during scrolling
    public void OnScroll(PointerEventData eventData)
    {
        UpdateCardScales();
    }
}
