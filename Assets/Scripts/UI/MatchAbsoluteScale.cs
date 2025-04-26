using UnityEngine;

/// <summary>
/// Matches this object's scale to match another object's absolute world scale
/// </summary>
/// <inheritdoc />
public class MatchAbsoluteScale : MonoBehaviour
{
    [SerializeField] [Tooltip("The GameObject whose absolute scale will be matched")] private GameObject targetObject;
    [SerializeField] [Tooltip("If true, will update the scale every frame. If false, will only set it once on Start")] private bool continuousUpdate;

    // Initializes scaling on startup if not doing continuous updates
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

    // Updates scale continuously if enabled
    private void Update()
    {
        if (continuousUpdate && targetObject)
            MatchScale();
    }

    // Calculates and applies the correct scale to match target's world scale
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
