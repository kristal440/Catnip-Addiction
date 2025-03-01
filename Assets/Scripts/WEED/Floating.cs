using UnityEngine;

public class Floating : MonoBehaviour
{
    public float amplitude = 0.5f; // The maximum distance the object will move up and down
    public float frequency = 1.0f; // The speed of the floating motion

    private Vector2 _initialPosition;

    private void Start()
    {
        _initialPosition = transform.position;
    }

    private void Update()
    {
        // sine wave
        var verticalOffset = Mathf.Sin(Time.time * frequency) * amplitude;

        // Update position
        transform.position = _initialPosition + new Vector2(0, verticalOffset);
    }
}
