using UnityEngine;

public class Floating : MonoBehaviour
{
    public float amplitude = 0.5f;
    public float frequency = 1.0f;

    private Vector2 _initialPosition;

    private void Start()
    {
        _initialPosition = transform.position;
    }

    private void Update()
    {
        var verticalOffset = Mathf.Sin(Time.time * frequency) * amplitude;

        transform.position = _initialPosition + new Vector2(0, verticalOffset);
    }
}
