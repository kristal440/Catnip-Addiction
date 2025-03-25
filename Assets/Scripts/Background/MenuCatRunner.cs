using UnityEngine;

public class MenuCatRunner : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int SkinParam = Animator.StringToHash("Skin");

    private float _speed;
    private float _destroyXPosition;
    private Animator _animator;
    private bool _hasJumped;
    private float _nextJumpTime;
    private float _minAnimSpeed;
    private float _maxAnimSpeed;

    public void Initialize(float speed, float destroyXPosition, int skinVariant, float minAnimSpeed = 4f, float maxAnimSpeed = 8f)
    {
        _speed = speed;
        _destroyXPosition = destroyXPosition;
        _hasJumped = false;
        _nextJumpTime = Random.Range(1f, 3f);
        _minAnimSpeed = minAnimSpeed;
        _maxAnimSpeed = maxAnimSpeed;

        if (!gameObject.activeInHierarchy)
            return;

        if (!_animator)
            _animator = GetComponent<Animator>();

        if (!_animator)
            return;

        _animator.SetInteger(SkinParam, skinVariant);

        var animSpeed = Random.Range(_minAnimSpeed, _maxAnimSpeed);
        _animator.SetFloat(Speed, animSpeed);

        var animationName = animSpeed <= 1f ? $"Cat-{skinVariant}-Walk" : $"Cat-{skinVariant}-Run";
        _animator.Play(animationName, 0, Random.Range(0f, 1f));
    }

    private void Update()
    {
        transform.Translate(Vector3.right * (_speed * Time.deltaTime));

        if (_animator && !_hasJumped && Time.time > _nextJumpTime && Random.value < 0.3f)
        {
            _animator.SetTrigger(JumpTrigger);
            _hasJumped = true;
        }

        if (transform.position.x > _destroyXPosition)
        {
            gameObject.SetActive(false);
        }
    }
}
