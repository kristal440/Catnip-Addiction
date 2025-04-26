using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the cat's meowing behavior, including animation, sound, and cooldown.
/// </summary>
/// <inheritdoc />
public class MeowController : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] [Tooltip("Animator component for meow animations")] private Animator meowAnimator;
    [SerializeField] [Tooltip("Sprite renderer for meow visual effects")] private SpriteRenderer meowRenderer;
    [SerializeField] [Tooltip("Audio source for playing meow sounds")] private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] [Tooltip("Time between possible meows in seconds")] private float meowCooldown = 2f;
    [SerializeField] [Tooltip("Keyboard key that triggers the meow action")] private Key meowKey = Key.M;
    [SerializeField] [Tooltip("Name of the animation to play when meowing")] private string meowAnimationName = "Meow";

    [Header("Sounds")]
    [SerializeField] [Tooltip("Array of possible meow sound clips to play randomly")] private AudioClip[] meowSounds;

    private InputAction _meowAction;
    private float _nextMeowTime;
    private PlayerController _playerController;
    private Button _meowButton;

    // Sets up input system, references, and components
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

    // Updates meow button interactivity based on cooldown
    private void Update()
    {
        if (!photonView.IsMine)
            return;

        if (_meowButton && !_meowButton.interactable && Time.time >= _nextMeowTime)
            _meowButton.interactable = true;
    }

    // Cleans up input actions and event listeners
    private void OnDestroy()
    {
        _meowAction?.Disable();
        _meowAction?.Dispose();

        _meowButton.onClick.RemoveListener(TryMeow);
    }

    // Attempts to trigger a meow if conditions are met
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

    // RPC method that plays meow animation and sound on all clients
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

    // Called by animation events to hide meow visual when complete
    public void OnMeowAnimationComplete()
    {
        if (meowRenderer != null)
            meowRenderer.enabled = false;
    }
}
