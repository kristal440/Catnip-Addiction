using UnityEngine;

public class MatchAbsoluteScale : MonoBehaviour
{
    [Tooltip("The GameObject whose absolute scale will be matched")]
    public GameObject targetObject;

    [Tooltip("If true, will update the scale every frame. If false, will only set it once on Start")]
    public bool continuousUpdate;

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("MatchAbsoluteScale: No target object assigned", this);
            return;
        }

        if (!continuousUpdate)
            MatchScale();
    }

    private void Update()
    {
        if (continuousUpdate && targetObject)
            MatchScale();
    }

    private void MatchScale()
    {
        var targetWorldScale = targetObject.transform.lossyScale;

        if (!transform.parent)
        {
            transform.localScale = targetWorldScale;
            return;
        }

        var parentWorldScale = transform.parent.lossyScale;

        var newLocalScale = new Vector3(
            parentWorldScale.x != 0 ? targetWorldScale.x / parentWorldScale.x : 0,
            parentWorldScale.y != 0 ? targetWorldScale.y / parentWorldScale.y : 0,
            parentWorldScale.z != 0 ? targetWorldScale.z / parentWorldScale.z : 0
        );

        transform.localScale = newLocalScale;
    }
}
