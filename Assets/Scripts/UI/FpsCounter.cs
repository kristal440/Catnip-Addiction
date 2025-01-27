using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;
    public float updateIntervalSeconds = 0.5f; // How often the FPS text updates

    private float _deltaTimeAccumulator;
    private int _frameCount;
    private float _currentFps;

    // Start is called before the first frame update
    private void Start()
    {
        if (fpsText == null)
        {
            Debug.LogError("FpsCounter: fpsText is not assigned! Please assign a TextMeshProUGUI component in the Inspector.");
            enabled = false; // Disable the script if fpsText is not assigned to prevent errors
            return;
        }

        DontDestroyOnLoad(gameObject); // Make this GameObject persistent between scenes (optional, depends on your needs)
    }

    // Update is called once per frame
    private void Update()
    {
        _deltaTimeAccumulator += Time.deltaTime;
        _frameCount++;

        // Update FPS text if enough time has passed
        if (!(_deltaTimeAccumulator > updateIntervalSeconds)) return;
        _currentFps = _frameCount / _deltaTimeAccumulator; // Calculate FPS

        // Update the TextMeshPro text
        if (fpsText != null) // Check again in case the object was somehow destroyed
        {
            fpsText.text = Mathf.RoundToInt(_currentFps) + "FPS"; // Round to integer for cleaner display
        }

        _deltaTimeAccumulator = 0f; // Reset accumulator
        _frameCount = 0;             // Reset frame count
    }
}