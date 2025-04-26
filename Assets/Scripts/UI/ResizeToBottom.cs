using UnityEngine;

/// <summary>
/// Resizes a UI element to maintain a specific spacing from the bottom of its container
/// </summary>
/// <inheritdoc />
public class ResizeToBottom : MonoBehaviour
{
    [SerializeField] [Tooltip("Space in pixels to maintain from the bottom edge")] private float spacing;

    // Updates the RectTransform to maintain proper spacing from bottom
    private void Update()
    {
        var rectTransform = GetComponent<RectTransform>();
        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, spacing);
        var offsetMax = rectTransform.offsetMax;
        offsetMax = new Vector2(offsetMax.x, offsetMax.y);
        rectTransform.offsetMax = offsetMax;
    }
}
