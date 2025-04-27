using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Displays network ping information for a player
/// </summary>
public class PlayerPing : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] [Tooltip("TextMeshPro component to display ping")] private TextMeshProUGUI pingText;
    [SerializeField] [Tooltip("The player's name tag TextMeshPro component")] private TextMeshProUGUI playerNameTag;
    [SerializeField] [Tooltip("The player GameObject this ping is attached to")] private Transform playerTransform;

    [Header("Display Settings")]
    [SerializeField] [Tooltip("Format string for ping display. Use {0} for ping value")] private string pingFormat = "{0} ms";
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("How often to update ping display (in seconds)")] private float updateInterval = 1.0f;
    [SerializeField] [Tooltip("Whether to show ping for all players or just the local player")] private bool showOnlyLocalPlayer = true;
    [SerializeField] [Tooltip("Whether to display ping at all")] private bool showPing = true;

    [Header("Position Settings")]
    [SerializeField] [Tooltip("Vertical offset from the player name tag")] private float verticalOffset = -0.5f;

    [Header("Visual Settings")]
    [SerializeField] [Tooltip("Enable color coding based on ping values")] private bool useColorCoding = true;
    [SerializeField] [Tooltip("Color for excellent ping")] private Color excellentPingColor = new(0.0f, 1.0f, 0.0f, 1.0f);
    [SerializeField] [Tooltip("Color for good ping")] private Color goodPingColor = new(1.0f, 1.0f, 0.0f, 1.0f);
    [SerializeField] [Tooltip("Color for poor ping")] private Color poorPingColor = new(1.0f, 0.5f, 0.0f, 1.0f);
    [SerializeField] [Tooltip("Color for bad ping")] private Color badPingColor = new(1.0f, 0.0f, 0.0f, 1.0f);
    [SerializeField] [Range(0, 999)] [Tooltip("Maximum ping value considered excellent (in milliseconds)")] private int excellentPingThreshold = 50;
    [SerializeField] [Range(0, 999)] [Tooltip("Maximum ping value considered good (in milliseconds)")] private int goodPingThreshold = 100;
    [SerializeField] [Range(0, 999)] [Tooltip("Maximum ping value considered poor (in milliseconds)")] private int poorPingThreshold = 200;

    [Header("Debug")]
    [SerializeField] [Tooltip("Enable debug mode")] private bool debugMode;
    [SerializeField] [Tooltip("Simulate ping value when in debug mode")] private int simulatedPing = 50;

    private int _currentPing;
    private Coroutine _updateCoroutine;
    private bool _initialized;
    private Vector3 _previousPlayerScale;

    #region Unity Lifecycle

    private void Awake()
    {
        _previousPlayerScale = playerTransform.localScale;
    }

    private void Start()
    {
        Initialize();

        if (_initialized && showPing)
            StartPingUpdates();
    }

    private void Update()
    {
        CheckPlayerScaleChange();
    }

    public override void OnEnable()
    {
        if (_initialized && showPing)
            StartPingUpdates();
    }

    public override void OnDisable()
    {
        StopPingUpdates();
    }

    #endregion

    #region Scale Handling

    /// Checks if player scale has changed and updates ping text scale accordingly
    private void CheckPlayerScaleChange()
    {
        if (!_initialized || !pingText || !playerTransform)
            return;

        if (Mathf.Approximately(_previousPlayerScale.x, playerTransform.localScale.x)) return;

        var pingScale = pingText.transform.localScale;
        pingScale.x = -pingScale.x;
        pingText.transform.localScale = pingScale;

        _previousPlayerScale = playerTransform.localScale;
    }

    #endregion

    #region Initialization

    /// Initialize the ping display
    private void Initialize()
    {
        if (_initialized)
            return;

        if (photonView == null)
        {
            Debug.LogError("PlayerPing: PhotonView component not found!");
            enabled = false;
            return;
        }

        if (pingText == null || playerNameTag == null || playerTransform == null)
            return;

        _initialized = true;
        ConfigureTextPosition();
    }

    /// Sets the position of the ping text relative to the player name tag
    private void ConfigureTextPosition()
    {
        if (pingText == null || playerNameTag == null) return;

        var pingTextRect = pingText.GetComponent<RectTransform>();
        var nameTagRect = playerNameTag.GetComponent<RectTransform>();

        if (pingTextRect == null || nameTagRect == null) return;

        var anchoredPosition = nameTagRect.anchoredPosition;
        pingTextRect.anchoredPosition = new Vector2(
            anchoredPosition.x,
            anchoredPosition.y + verticalOffset
        );
    }

    #endregion

    #region Ping Updates

    /// Start the ping update coroutine
    private void StartPingUpdates()
    {
        StopPingUpdates();
        _updateCoroutine = StartCoroutine(UpdatePingRoutine());
    }

    /// Stop the ping update coroutine
    private void StopPingUpdates()
    {
        if (_updateCoroutine == null) return;

        StopCoroutine(_updateCoroutine);
        _updateCoroutine = null;
    }

    /// Coroutine that updates the ping display at regular intervals
    private IEnumerator UpdatePingRoutine()
    {
        var wait = new WaitForSeconds(updateInterval);

        while (enabled && showPing)
        {
            UpdatePingDisplay();
            yield return wait;
        }
    }

    /// Update the ping text display with the current ping value
    private void UpdatePingDisplay()
    {
        if (!_initialized || !pingText)
            return;

        if (showOnlyLocalPlayer && !photonView.IsMine)
        {
            pingText.gameObject.SetActive(false);
            return;
        }

        _currentPing = GetCurrentPing();

        var formattedPing = string.Format(pingFormat, _currentPing);
        pingText.text = formattedPing;

        if (useColorCoding)
            pingText.color = GetPingColor(_currentPing);

        pingText.gameObject.SetActive(showPing);
    }

    /// Get the current ping value
    private int GetCurrentPing()
    {
        return debugMode ? simulatedPing : PhotonNetwork.GetPing();
    }

    /// Determine text color based on ping value
    private Color GetPingColor(int ping)
    {
        if (ping <= excellentPingThreshold)
            return excellentPingColor;
        if (ping <= goodPingThreshold)
            return goodPingColor;

        return ping <= poorPingThreshold ? poorPingColor : badPingColor;
    }

    #endregion
}
