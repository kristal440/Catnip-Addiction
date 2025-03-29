using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class StylusInputBlocker : MonoBehaviour
{
    [Header("Blocking Settings")]
    [SerializeField] private bool blockPenDevices = true;
    [SerializeField] private bool blockStylusTouches = true;
    [SerializeField] private bool blockTabletInput = true;

    [Header("Debug")]
    [SerializeField] private bool logBlockedEvents;
    [SerializeField] private int maxLoggedEvents = 100;

    private int _loggedEventsCount;
    private readonly HashSet<InputDevice> _identifiedStylusDevices = new();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        InputSystem.onEvent += FilterStylusEvents;
        InputSystem.onDeviceChange += OnDeviceChange;
        Debug.Log("Stylus input blocker initialized");
    }

    private void OnDestroy()
    {
        InputSystem.onEvent -= FilterStylusEvents;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            if (change is InputDeviceChange.Removed or InputDeviceChange.Disconnected)
            {
                _identifiedStylusDevices.Remove(device);
            }
        }
        else
        {
            if (!IsStylusDevice(device)) return;
            _identifiedStylusDevices.Add(device);
            if (logBlockedEvents)
                Debug.Log($"Identified stylus device: {device.name}");
        }
    }

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
        if (stylusKeywords.Any(keyword => deviceName.Contains(keyword) || productName.Contains(keyword)))
        {
            return true;
        }

        return device.description.interfaceName == "HID" &&
               !string.IsNullOrEmpty(device.description.capabilities) &&
               (device.description.capabilities.Contains("Tablet") ||
                device.description.capabilities.Contains("Digitizer"));
    }

    private void FilterStylusEvents(InputEventPtr eventPtr, InputDevice device)
    {
        // Early return if device is null
        if (device == null)
        {
            if (!logBlockedEvents || _loggedEventsCount >= maxLoggedEvents) return;
            Debug.Log("Received input event with null device");
            _loggedEventsCount++;
            return;
        }

        var shouldBlock = false;
        var blockReason = string.Empty;

        // Block known stylus devices
        if (blockPenDevices && (_identifiedStylusDevices.Contains(device) || device is Pen))
        {
            shouldBlock = true;
            blockReason = "Known stylus device";
        }
        // Check for stylus touch events
        else if (blockStylusTouches && eventPtr.type == TouchState.Format && IsStylusTouchEvent(eventPtr))
        {
            shouldBlock = true;
            blockReason = "Stylus touch event";

            // Remember this device for future events
            _identifiedStylusDevices.Add(device);
        }
        // Block tablet devices
        else if (blockTabletInput && IsStylusDevice(device))
        {
            shouldBlock = true;
            blockReason = "Tablet input device";
        }

        if (!shouldBlock) return;
        // Mark the event as handled to prevent propagation
        eventPtr.handled = true;

        // Log blocked events if enabled
        if (!logBlockedEvents || _loggedEventsCount >= maxLoggedEvents) return;
        Debug.Log($"Blocked stylus input: {blockReason}, Device: {device.name}");
        _loggedEventsCount++;

        if (_loggedEventsCount == maxLoggedEvents)
            Debug.Log("Maximum logged stylus events reached. Further logging suppressed.");
    }

    private static bool IsStylusTouchEvent(InputEventPtr eventPtr)
    {
        unsafe
        {
            var touchState = (TouchState*)eventPtr.data;

            // Replace TouchFlags.Pen check with bit flag check
            // TouchFlags.Pen is typically bit 2 (value 4)
            // Using raw value 4 instead of the inaccessible enum
            if ((touchState->flags & 4) != 0)
                return true;

            // Check for negative touchId/phaseId which can indicate stylus on some devices
            if (touchState->touchId < 0)
                return true;

            // Check pressure characteristics - stylus often has precise pressure
            if (touchState->pressure > 0 && touchState->pressure < 0.1f)
                return true;

            // Check touch radius - stylus typically has small contact area
            if (touchState->radius.x < 0.01f && touchState->radius.y < 0.01f && touchState->pressure > 0)
                return true;

            // Check for specific flag patterns that might indicate stylus
            return touchState->flags > 10;
        }
    }

    #if UNITY_EDITOR
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
