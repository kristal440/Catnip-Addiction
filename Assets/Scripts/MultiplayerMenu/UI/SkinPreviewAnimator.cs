using UnityEngine;

/// <summary>
/// Manages the animation of skin previews in the UI by controlling animator states based on skin selection.
/// </summary>
/// <inheritdoc />
public class SkinPreviewAnimator : MonoBehaviour
{
    private const string BaseLayerName = "Base Layer";
    private const string IdleStateSuffix = "-Idle";
    private static readonly int Skin = Animator.StringToHash("Skin");

    private Animator _animator;
    private int _baseLayerIndex;
    private string _currentSkinName;

    // Initializes component references and verifies required components exist
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("SkinPreviewAnimator requires an Animator component on the same GameObject!");
            enabled = false;
            return;
        }

        _baseLayerIndex = _animator.GetLayerIndex(BaseLayerName);
        if (_baseLayerIndex != -1) return;

        Debug.LogError($"Base layer '{BaseLayerName}' not found in Animator Controller!");
        enabled = false;
    }

    // Sets the current skin name and updates the animation accordingly
    internal void SetSkinName(string skinName)
    {
        _currentSkinName = skinName;
        UpdateAnimation();
    }

    // Updates the animator to play the appropriate animation for the current skin
    private void UpdateAnimation()
    {
        if (_animator == null || string.IsNullOrEmpty(_currentSkinName)) return;

        var stateName = $"{_currentSkinName}{IdleStateSuffix}";
        var stateHash = Animator.StringToHash(stateName);

        _animator.SetInteger(Skin, GetSkinIndex(_currentSkinName));

        if (_animator.HasState(_baseLayerIndex, stateHash))
            _animator.Play(stateHash, _baseLayerIndex);
        else
            Debug.LogError($"State '{stateName}' not found in Animator Controller for skin: {_currentSkinName} on layer '{BaseLayerName}' (index: {_baseLayerIndex})");
    }

    // Converts skin name to corresponding integer index for animator parameter
    private static int GetSkinIndex(string skinName)
    {
        return skinName.ToLower() switch
        {
            "cat-1" => 0,
            "cat-2" => 1,
            "cat-6" => 2,
            _ => 0
        };
    }
}
