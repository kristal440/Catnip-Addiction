using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerNotificationManager : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public TMP_Text playerNameText;
    public TMP_Text statusText;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.5f;
    public float displayDuration = 2.5f;
    public float fadeOutDuration = 0.5f;
    public float slideDistance = 50f;

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

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.anchoredPosition;

        _canvasGroup.alpha = 0f;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        QueueNotification(newPlayer.NickName, true);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        QueueNotification(otherPlayer.NickName, false);
    }

    private void QueueNotification(string playerName, bool isJoining)
    {
        _notificationQueue.Enqueue(new NotificationInfo { PlayerName = playerName, IsJoining = isJoining });

        if (!_isProcessingQueue)
            StartCoroutine(ProcessNotificationQueue());
    }

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
