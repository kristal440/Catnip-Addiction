using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <inheritdoc />
/// <summary>
/// Blocks and filters input from stylus and pen devices to prevent unwanted interactions.
/// </summary>
public class StylusInputBlocker : MonoBehaviour
{
    [Header("Blocking Settings")]
    [SerializeField] [Tooltip("When enabled, blocks input from pen-type devices")] private bool blockPenDevices = true;
    [SerializeField] [Tooltip("When enabled, blocks touch events identified as coming from a stylus")] private bool blockStylusTouches = true;
    [SerializeField] [Tooltip("When enabled, blocks input from tablet devices")] private bool blockTabletInput = true;

    [Header("Debug")]
    [SerializeField] [Tooltip("Logs details about blocked stylus events to the console")] private bool logBlockedEvents;
    [SerializeField] [Tooltip("Maximum number of events to log before suppressing further logging")] private int maxLoggedEvents = 100;

    private int _loggedEventsCount;
    private readonly HashSet<InputDevice> _identifiedStylusDevices = new();

    // Initializes the input blocker and registers event handlers
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        InputSystem.onEvent += FilterStylusEvents;
        InputSystem.onDeviceChange += OnDeviceChange;
        Debug.Log("Stylus input blocker initialized");
    }

    // Cleans up event handlers when object is destroyed
    private void OnDestroy()
    {
        InputSystem.onEvent -= FilterStylusEvents;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // Monitors device connections to identify and track stylus devices
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            if (change is InputDeviceChange.Removed or InputDeviceChange.Disconnected)
                _identifiedStylusDevices.Remove(device);
        }
        else
        {
            if (!IsStylusDevice(device)) return;

            _identifiedStylusDevices.Add(device);
            if (logBlockedEvents)
                Debug.Log($"Identified stylus device: {device.name}");
        }
    }

    // Determines if a device is a stylus based on its name and capabilities
    private static bool IsStylusDevice(InputDevice device)
    {
        switch (device)
        {
            case null:
                return false;
            case Pen:
                return true;
        }

        var deviceName = device.name?.ToLowerInvariant() ?? string.Empty;
        var productName = device.description.product?.ToLowerInvariant() ?? string.Empty;

        string[] stylusKeywords = { "pen", "stylus", "wacom", "bamboo", "digitizer" };
        if (stylusKeywords.Any(keyword => deviceName.Contains(keyword) || productName.Contains(keyword))) return true;

        return device.description.interfaceName == "HID" &&
               !string.IsNullOrEmpty(device.description.capabilities) &&
               (device.description.capabilities.Contains("Tablet") ||
                device.description.capabilities.Contains("Digitizer"));
    }

    // Intercepts and blocks input events from stylus devices based on configuration
    private void FilterStylusEvents(InputEventPtr eventPtr, InputDevice device)
    {
        if (device == null)
        {
            if (!logBlockedEvents || _loggedEventsCount >= maxLoggedEvents) return;

            Debug.Log("Received input event with null device");
            _loggedEventsCount++;
            return;
        }

        var shouldBlock = false;
        var blockReason = string.Empty;

        if (blockPenDevices && (_identifiedStylusDevices.Contains(device) || device is Pen))
        {
            shouldBlock = true;
            blockReason = "Known stylus device";
        }
        else if (blockStylusTouches && eventPtr.type == TouchState.Format && IsStylusTouchEvent(eventPtr))
        {
            shouldBlock = true;
            blockReason = "Stylus touch event";

            _identifiedStylusDevices.Add(device);
        }
        else if (blockTabletInput && IsStylusDevice(device))
        {
            shouldBlock = true;
            blockReason = "Tablet input device";
        }

        if (!shouldBlock) return;

        eventPtr.handled = true;

        if (!logBlockedEvents || _loggedEventsCount >= maxLoggedEvents) return;

        Debug.Log($"Blocked stylus input: {blockReason}, Device: {device.name}");
        _loggedEventsCount++;

        if (_loggedEventsCount == maxLoggedEvents)
            Debug.Log("Maximum logged stylus events reached. Further logging suppressed.");
    }

    // Identifies touch events that originate from a stylus
    private static bool IsStylusTouchEvent(InputEventPtr eventPtr)
    {
        unsafe
        {
            var touchState = (TouchState*)eventPtr.data;

            if ((touchState->flags & 4) != 0)
                return true;

            if (touchState->touchId < 0)
                return true;

            if (touchState->pressure > 0 && touchState->pressure < 0.1f)
                return true;

            if (touchState->radius.x < 0.01f && touchState->radius.y < 0.01f && touchState->pressure > 0)
                return true;

            return touchState->flags > 10;
        }
    }

    #if UNITY_EDITOR
    // Logs all connected input devices for debugging purposes
    [ContextMenu("Log Connected Input Devices")]
    private void LogConnectedDevices()
    {
        Debug.Log("Connected Input Devices:");
        foreach (var device in InputSystem.devices)
        {
            var stylusIndicator = IsStylusDevice(device) ? " [STYLUS]" : "";
            Debug.Log($"- {device.name} ({device.GetType().Name}){stylusIndicator}: {device.description.product}");
        }
    }
    #endif
}
