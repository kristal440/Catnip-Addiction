using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Controls background cat animations that run across the menu screen.
/// </summary>
public class MenuCatRunner : MonoBehaviour
{
    [SerializeField] [Tooltip("Reference to the cat animator component")] private Animator animator;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int SkinParam = Animator.StringToHash("Skin");

    private float _speed;
    private float _destroyXPosition;

    /// Sets up cat runner with movement and animation parameters
    internal void Initialize(float speed, float destroyXPosition, int skinVariant,
        float minAnimSpeed, float maxAnimSpeed)
    {
        _speed = speed;
        _destroyXPosition = destroyXPosition;

        if (!gameObject.activeInHierarchy) return;

        animator.SetInteger(SkinParam, skinVariant);

        var animSpeed = Random.Range(minAnimSpeed, maxAnimSpeed);
        animator.SetFloat(Speed, animSpeed);
    }

    /// Moves the cat horizontally and disables it when off-screen
    private void Update()
    {
        transform.Translate(Vector3.right * (_speed * Time.deltaTime));

        if (!(transform.position.x > _destroyXPosition)) return;

        gameObject.SetActive(false);
    }

    /// Animation event callback for standing animation completion
    public void OnStandingAnimationComplete() { }

    /// Pauses jump animation at a specific frame
    public void PauseJump() { }
}
