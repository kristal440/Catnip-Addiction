using UnityEngine;
using TMPro;

/// <inheritdoc />
/// <summary>
/// Component that makes TMP_InputField values persist between scene loads.
/// </summary>
public class RememberInput : MonoBehaviour
{
    private TMP_InputField _inputField;
    private string _uniqueKey;

    /// Initializes component and sets up input field listeners
    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();

        if (_inputField == null)
        {
            Debug.LogError("RememberInput script needs to be attached to a GameObject with a TMP_InputField component");
            return;
        }

        _uniqueKey = "Input_" + GetFullPath(transform);

        _inputField.onValueChanged.AddListener(SaveInputValue);
        _inputField.onEndEdit.AddListener(SaveInputValue);
    }

    /// Loads saved input value on start
    private void Start()
    {
        LoadInputValue();
    }

    /// Saves input field value to PlayerPrefs
    private void SaveInputValue(string value)
    {
        PlayerPrefs.SetString(_uniqueKey, value);
        PlayerPrefs.Save();
    }

    /// Loads saved input value from PlayerPrefs if available
    private void LoadInputValue()
    {
        if (!PlayerPrefs.HasKey(_uniqueKey)) return;

        var savedValue = PlayerPrefs.GetString(_uniqueKey);
        _inputField.text = savedValue;
    }

    /// Creates a unique path string based on the GameObject's hierarchy
    private static string GetFullPath(Transform transformObj)
    {
        var path = transformObj.name;
        var parent = transformObj.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    /// Removes listeners when component is destroyed
    private void OnDestroy()
    {
        if (_inputField == null) return;

        _inputField.onValueChanged.RemoveListener(SaveInputValue);
        _inputField.onEndEdit.RemoveListener(SaveInputValue);
    }
}
