using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages and displays notifications when players join or leave the room with animated UI elements.
/// </summary>
/// <inheritdoc />
public class PlayerNotificationManager : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] [Tooltip("Text element displaying the player's name")] public TMP_Text playerNameText;
    [SerializeField] [Tooltip("Text element displaying the player's status (joined/left)")] public TMP_Text statusText;

    [Header("Animation Settings")]
    [SerializeField] [Tooltip("Duration of the fade-in animation")] public float fadeInDuration = 0.5f;
    [SerializeField] [Tooltip("How long the notification stays visible")] public float displayDuration = 2.5f;
    [SerializeField] [Tooltip("Duration of the fade-out animation")] public float fadeOutDuration = 0.5f;
    [SerializeField] [Tooltip("Vertical distance the notification slides during animations")] public float slideDistance = 50f;

    [Header("Death Notification Settings")]
    [SerializeField] [Tooltip("Color for player names in death notifications")] private Color deathNameColor = new(1f, 0.5f, 0.5f);
    [SerializeField] [Tooltip("Color for death messages")] private Color deathMessageColor = new(1f, 0.3f, 0.3f);

    [Header("Finish Notification Settings")]
    [SerializeField] [Tooltip("Color for player names in finish notifications")] private Color finishNameColor = Color.white;
    [SerializeField] [Tooltip("Color for finish messages")] private Color finishMessageColor = Color.paleGreen;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector2 _originalPosition;
    private Vector2 _startPosition;
    private Vector2 _endPosition;
    private readonly Queue<NotificationInfo> _notificationQueue = new();
    private bool _isProcessingQueue;

    private struct NotificationInfo
    {
        public string PlayerName;
        public string Message;
        public NotificationType Type;
        public float CustomDisplayDuration;
    }

    private enum NotificationType
    {
        PlayerJoined,
        PlayerLeft,
        PlayerDeath,
        PlayerFinished
    }

    /// Initializes required components and sets initial state
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.anchoredPosition;

        _startPosition = _originalPosition - new Vector2(0, slideDistance);
        _endPosition = _originalPosition + new Vector2(0, slideDistance);

        _canvasGroup.alpha = 0f;
    }

    /// <inheritdoc />
    /// Called when a new player joins the room
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        QueueNotification(newPlayer.NickName, "joined", NotificationType.PlayerJoined);
    }

    /// <inheritdoc />
    /// Called when a player leaves the room
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        QueueNotification(otherPlayer.NickName, "left", NotificationType.PlayerLeft);
    }

    /// Shows a death notification for the specified player
    internal void ShowDeathNotification(string playerName)
    {
        QueueNotification(playerName, "died.", NotificationType.PlayerDeath);
    }

    /// Shows a finish notification for the specified player
    internal void ShowFinishNotification(string playerName)
    {
        QueueNotification(playerName, "finished", NotificationType.PlayerFinished);
    }

    /// Adds a notification to the queue and starts processing if needed
    private void QueueNotification(string playerName, string message, NotificationType type)
    {
        _notificationQueue.Enqueue(new NotificationInfo
        {
            PlayerName = playerName,
            Message = message,
            Type = type
        });

        if (!_isProcessingQueue)
            StartCoroutine(ProcessNotificationQueue());
    }

    /// Processes all queued notifications sequentially
    private IEnumerator ProcessNotificationQueue()
    {
        _isProcessingQueue = true;

        while (_notificationQueue.Count > 0)
        {
            var notification = _notificationQueue.Dequeue();

            playerNameText.text = notification.PlayerName;
            statusText.text = notification.Message;

            switch (notification.Type)
            {
                case NotificationType.PlayerJoined:
                    statusText.color = Color.green;
                    playerNameText.color = Color.white;
                    break;
                case NotificationType.PlayerLeft:
                    statusText.color = Color.red;
                    playerNameText.color = Color.white;
                    break;
                case NotificationType.PlayerDeath:
                    statusText.color = deathMessageColor;
                    playerNameText.color = deathNameColor;
                    break;
                case NotificationType.PlayerFinished:
                    statusText.color = finishMessageColor;
                    playerNameText.color = finishNameColor;
                    break;
            }

            yield return StartCoroutine(AnimateNotification(notification.CustomDisplayDuration));

            yield return new WaitForSeconds(0.2f);
        }

        _isProcessingQueue = false;
    }

    /// Handles the animation sequence for a notification
    private IEnumerator AnimateNotification(float customDisplayDuration = -1)
    {
        var actualDisplayDuration = customDisplayDuration > 0 ? customDisplayDuration : displayDuration;

        _canvasGroup.alpha = 0f;
        float time = 0;
        while (time < fadeInDuration)
        {
            var t = time / fadeInDuration;
            _canvasGroup.alpha = t;
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _startPosition,
                _originalPosition,
                t
            );
            time += Time.deltaTime;
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _rectTransform.anchoredPosition = _originalPosition;

        yield return new WaitForSeconds(actualDisplayDuration);

        time = 0;
        while (time < fadeOutDuration)
        {
            var t = time / fadeOutDuration;
            _canvasGroup.alpha = 1 - t;
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _originalPosition,
                _endPosition,
                t
            );
            time += Time.deltaTime;
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
