using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

public class MeowController : MonoBehaviourPunCallbacks
{
    [Header("Meow Settings")]
    [SerializeField] private Animator meowAnimator;
    [SerializeField] private SpriteRenderer meowRenderer;
    [SerializeField] private Key meowKey = Key.M;
    [SerializeField] private float meowCooldown = 2f;
    [SerializeField] private string meowAnimationName = "Meow";

    private InputAction _meowAction;
    private float _nextMeowTime;

    private void Awake()
    {
        // Setup input binding
        _meowAction = new InputAction("Meow", InputActionType.Button);
        _meowAction.AddBinding("<Keyboard>/" + meowKey.ToString().ToLower());
        _meowAction.performed += _ => TryMeow();
        _meowAction.Enable();

        if (meowRenderer != null)
            meowRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        _meowAction?.Disable();
        _meowAction?.Dispose();
    }

    private void TryMeow()
    {
        if (!photonView.IsMine) return;
        if (Time.time < _nextMeowTime) return;

        _nextMeowTime = Time.time + meowCooldown;
        photonView.RPC(nameof(RPC_PlayMeow), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_PlayMeow()
    {
        if (meowRenderer == null || meowAnimator == null) return;

        meowRenderer.enabled = true;
        meowAnimator.Play(meowAnimationName, 0, 0f);
    }

    public void OnMeowAnimationComplete()
    {
        if (meowRenderer != null)
            meowRenderer.enabled = false;
    }
}
