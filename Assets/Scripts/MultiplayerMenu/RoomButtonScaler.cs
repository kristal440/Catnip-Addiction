using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RoomButtonScaler : MonoBehaviour, IScrollHandler, IBeginDragHandler, IEndDragHandler
{
    public RectTransform viewport;
    public float minScale = 0.7f;
    public float normalScale = 1f;

    private VerticalLayoutGroup layoutGroup;
    private RectTransform contentRect;
    private float viewportCenterY;

    private void Start()
    {
        layoutGroup = GetComponent<VerticalLayoutGroup>();
        contentRect = GetComponent<RectTransform>();

        if (viewport == null)
        {
            Debug.LogError("Viewport not assigned in RoomCardScaler!");
            return;
        }

        viewportCenterY = viewport.GetComponent<RectTransform>().position.y;
    }

    private void Update()
    {
        UpdateCardScales();
    }

    private void UpdateCardScales()
    {
        if (layoutGroup == null || viewport == null) return;

        Vector3 viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);

        foreach (Transform child in transform)
        {
            RectTransform childRect = child.GetComponent<RectTransform>();
            if (childRect != null)
            {
                Vector3 childCenterWorld = childRect.TransformPoint(childRect.rect.center);
                float distance = Mathf.Abs(childCenterWorld.y - viewportCenterWorld.y);
                float normalizedDistance = Mathf.Clamp01(distance / (viewport.rect.height / 2f));
                float scaleFactor = Mathf.Lerp(normalScale, minScale, normalizedDistance);

                // Apply the horizontal scale
                childRect.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }
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
