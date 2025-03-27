using UnityEngine;

public class MenuCatRunner : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int SkinParam = Animator.StringToHash("Skin");

    private float _speed;
    private float _destroyXPosition;
    private Animator _animator;
    private float _minAnimSpeed;
    private float _maxAnimSpeed;

    public void Initialize(float speed, float destroyXPosition, int skinVariant, float minAnimSpeed = 4f, float maxAnimSpeed = 8f)
    {
        _speed = speed;
        _destroyXPosition = destroyXPosition;
        _minAnimSpeed = minAnimSpeed;
        _maxAnimSpeed = maxAnimSpeed;

        if (!gameObject.activeInHierarchy) return;

        if (!_animator)
            _animator = GetComponent<Animator>();

        if (!_animator) return;

        _animator.SetInteger(SkinParam, skinVariant);

        var animSpeed = Random.Range(_minAnimSpeed, _maxAnimSpeed);
        _animator.SetFloat(Speed, animSpeed);

        var animationName = animSpeed <= 1f ? $"Cat-{skinVariant}-Walk" : $"Cat-{skinVariant}-Run";
        _animator.Play(animationName, 0, Random.Range(0f, 1f));
    }

    private void Update()
    {
        transform.Translate(Vector3.right * (_speed * Time.deltaTime));

        if (!(transform.position.x > _destroyXPosition)) return;
        gameObject.SetActive(false);
    }

    public void OnStandingAnimationComplete() { }
    public void PauseJump() { }
}
