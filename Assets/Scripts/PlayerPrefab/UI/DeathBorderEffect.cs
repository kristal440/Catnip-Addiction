using UnityEngine;
using UnityEngine.UI;

public class DeathBorderEffect : MonoBehaviour
{
    [SerializeField] private Image borderImage;
    [SerializeField] private float fadeSpeed = 2.0f;
    [SerializeField] private float maxAlpha = 0.8f;

    private float _currentAlpha;
    private bool _shouldShow;

    private void Start()
    {
        SetBorderAlpha(0);
    }

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

    public void ShowDeathBorder()
    {
        _shouldShow = true;
    }

    public void HideDeathBorder()
    {
        _shouldShow = false;
    }

    private void SetBorderAlpha(float alpha)
    {
        if (!borderImage) return;
        var color = borderImage.color;
        color.a = alpha;
        borderImage.color = color;
    }
}