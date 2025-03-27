using UnityEngine;

public class MenuCatRunner : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int SkinParam = Animator.StringToHash("Skin");

    private float _speed;
    private float _destroyXPosition;

    public void Initialize(float speed, float destroyXPosition, int skinVariant,
                          float minAnimSpeed, float maxAnimSpeed)
    {
        _speed = speed;
        _destroyXPosition = destroyXPosition;

        if (!gameObject.activeInHierarchy) return;

        animator.SetInteger(SkinParam, skinVariant);

        var animSpeed = Random.Range(minAnimSpeed, maxAnimSpeed);
        animator.SetFloat(Speed, animSpeed);
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
