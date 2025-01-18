using UnityEngine;

public class ResizeToBottom : MonoBehaviour
{
    public float spacing;

    void Update()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();

        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, spacing);
        rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, rectTransform.offsetMax.y);
    }
}
