using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;
    public float updateIntervalSeconds = 0.5f;

    private float _deltaTimeAccumulator;
    private int _frameCount;
    private float _currentFps;

    private void Start()
    {
        if (fpsText != null) return;
        enabled = false;
    }

    private void Update()
    {
        _deltaTimeAccumulator += Time.deltaTime;
        _frameCount++;

        if (!(_deltaTimeAccumulator > updateIntervalSeconds)) return;
        _currentFps = _frameCount / _deltaTimeAccumulator;

        if (fpsText)
        {
            fpsText.text = Mathf.RoundToInt(_currentFps) + "FPS";
        }

        _deltaTimeAccumulator = 0f;
        _frameCount = 0;
    }
}
