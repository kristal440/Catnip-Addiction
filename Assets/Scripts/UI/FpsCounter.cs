using TMPro;
using UnityEngine;

/// <summary>
/// Displays current frames per second on a TextMeshProUGUI component
/// </summary>
/// <inheritdoc />
public class FpsCounter : MonoBehaviour
{
    [SerializeField] [Tooltip("Text component where the FPS will be displayed")] private TextMeshProUGUI fpsText;
    [SerializeField] [Tooltip("How often to update the FPS display, in seconds")] private float updateIntervalSeconds = 0.5f;

    private float _deltaTimeAccumulator;
    private int _frameCount;
    private float _currentFps;

    /// Validates requirements and disables if text component is missing
    private void Start()
    {
        if (fpsText != null) return;

        enabled = false;
    }

    /// Calculates and updates the FPS display at specified intervals
    private void Update()
    {
        _deltaTimeAccumulator += Time.deltaTime;
        _frameCount++;

        if (!(_deltaTimeAccumulator > updateIntervalSeconds)) return;

        _currentFps = _frameCount / _deltaTimeAccumulator;

        if (fpsText)
            fpsText.text = Mathf.RoundToInt(_currentFps) + "FPS";

        _deltaTimeAccumulator = 0f;
        _frameCount = 0;
    }
}
