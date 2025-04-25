using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MeowController : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private Animator meowAnimator;
    [SerializeField] private SpriteRenderer meowRenderer;
    [SerializeField] private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] private float meowCooldown = 2f;
    [SerializeField] private Key meowKey = Key.M;
    [SerializeField] private string meowAnimationName = "Meow";

    [Header("Sounds")]
    [SerializeField] private AudioClip[] meowSounds;

    private InputAction _meowAction;
    private float _nextMeowTime;
    private PlayerController _playerController;
    private Button _meowButton;

    private void Awake()
    {
        _meowAction = new InputAction("Meow", InputActionType.Button);
        _meowAction.AddBinding("<Keyboard>/" + meowKey.ToString().ToLower());
        _meowAction.performed += _ => TryMeow();
        _meowAction.Enable();

        if (meowRenderer != null)
            meowRenderer.enabled = false;

        _playerController = GetComponentInParent<PlayerController>();

        _meowButton = GameObject.Find("MeowBtn").GetComponent<Button>();
        _meowButton.onClick.AddListener(TryMeow);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        if (_meowButton && !_meowButton.interactable && Time.time >= _nextMeowTime)
            _meowButton.interactable = true;
    }

    private void OnDestroy()
    {
        _meowAction?.Disable();
        _meowAction?.Dispose();

        _meowButton.onClick.RemoveListener(TryMeow);
    }

    private void TryMeow()
    {
        if (_playerController.IsPaused)
            return;

        if (!photonView.IsMine)
            return;

        if (Time.time < _nextMeowTime)
            return;

        if (!_playerController.IsStanding)
            return;

        _nextMeowTime = Time.time + meowCooldown;

        _meowButton.interactable = false;

        photonView.RPC(nameof(RPC_PlayMeow), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_PlayMeow()
    {
        if (meowRenderer != null && meowAnimator != null)
        {
            meowRenderer.enabled = true;
            meowAnimator.Play(meowAnimationName, 0, 0f);
        }

        if (meowSounds is not { Length: > 0 } || audioSource == null) return;

        var randomMeow = meowSounds[Random.Range(0, meowSounds.Length)];
        audioSource.PlayOneShot(randomMeow);
    }

    public void OnMeowAnimationComplete()
    {
        if (meowRenderer != null)
            meowRenderer.enabled = false;
    }
}
