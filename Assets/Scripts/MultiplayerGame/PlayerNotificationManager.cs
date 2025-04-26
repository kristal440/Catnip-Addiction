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

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector2 _originalPosition;
    private readonly Queue<NotificationInfo> _notificationQueue = new();
    private bool _isProcessingQueue;

    private struct NotificationInfo
    {
        public string PlayerName;
        public bool IsJoining;
    }

    // Initializes required components and sets initial state
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.anchoredPosition;

        _canvasGroup.alpha = 0f;
    }

    // Called when a new player joins the room
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        QueueNotification(newPlayer.NickName, true);
    }

    // Called when a player leaves the room
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        QueueNotification(otherPlayer.NickName, false);
    }

    // Adds a notification to the queue and starts processing if needed
    private void QueueNotification(string playerName, bool isJoining)
    {
        _notificationQueue.Enqueue(new NotificationInfo { PlayerName = playerName, IsJoining = isJoining });

        if (!_isProcessingQueue)
            StartCoroutine(ProcessNotificationQueue());
    }

    // Processes all queued notifications sequentially
    private IEnumerator ProcessNotificationQueue()
    {
        _isProcessingQueue = true;

        while (_notificationQueue.Count > 0)
        {
            var notification = _notificationQueue.Dequeue();

            playerNameText.text = notification.PlayerName;
            statusText.text = notification.IsJoining ? "joined" : "left";
            statusText.color = notification.IsJoining ? Color.green : Color.red;

            yield return StartCoroutine(AnimateNotification());

            yield return new WaitForSeconds(0.2f);
        }

        _isProcessingQueue = false;
    }

    // Handles the animation sequence for a notification
    private IEnumerator AnimateNotification()
    {
        _rectTransform.anchoredPosition = _originalPosition - new Vector2(0, slideDistance);

        _canvasGroup.alpha = 0f;
        float time = 0;
        while (time < fadeInDuration)
        {
            var t = time / fadeInDuration;
            _canvasGroup.alpha = t;
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _originalPosition - new Vector2(0, slideDistance),
                _originalPosition,
                t
            );
            time += Time.deltaTime;
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _rectTransform.anchoredPosition = _originalPosition;

        yield return new WaitForSeconds(displayDuration);

        time = 0;
        while (time < fadeOutDuration)
        {
            var t = time / fadeOutDuration;
            _canvasGroup.alpha = 1 - t;
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _originalPosition,
                _originalPosition + new Vector2(0, slideDistance),
                t
            );
            time += Time.deltaTime;
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
