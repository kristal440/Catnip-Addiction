using UnityEngine;

public class ResizeToBottom : MonoBehaviour
{
    public float spacing;

    private void Update()
    {
        var rectTransform = GetComponent<RectTransform>();
        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, spacing);
        var offsetMax = rectTransform.offsetMax;
        offsetMax = new Vector2(offsetMax.x, offsetMax.y);
        rectTransform.offsetMax = offsetMax;
    }
}
