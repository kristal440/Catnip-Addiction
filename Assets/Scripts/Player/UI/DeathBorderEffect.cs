using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Controls a visual border effect that fades in/out when the player is near death.
/// </summary>
public class DeathBorderEffect : MonoBehaviour
{
    [SerializeField] [Tooltip("Image component for the border effect")] private Image borderImage;
    [SerializeField] [Tooltip("Speed at which the border fades in/out")] private float fadeSpeed = 2.0f;
    [SerializeField] [Tooltip("Maximum opacity of the border effect")] private float maxAlpha = 0.8f;

    private float _currentAlpha;
    private bool _shouldShow;

    // Initializes the border effect with zero opacity
    private void Update()
    {
        switch (_shouldShow)
        {
            case true when _currentAlpha < maxAlpha:
            {
                _currentAlpha += Time.deltaTime * fadeSpeed;
                if (_currentAlpha > maxAlpha) _currentAlpha = maxAlpha;
                break;
            }
            case false when _currentAlpha > 0:
            {
                _currentAlpha -= Time.deltaTime * fadeSpeed;
                if (_currentAlpha < 0) _currentAlpha = 0;
                break;
            }
        }

        SetBorderAlpha(_currentAlpha);
    }

    // Activates the death border effect
    internal void ShowDeathBorder()
    {
        _shouldShow = true;
    }

    // Deactivates the death border effect
    internal void HideDeathBorder()
    {
        _shouldShow = false;
    }

    // Updates the border image opacity
    private void SetBorderAlpha(float alpha)
    {
        if (!borderImage) return;

        var color = borderImage.color;
        color.a = alpha;
        borderImage.color = color;
    }
}
