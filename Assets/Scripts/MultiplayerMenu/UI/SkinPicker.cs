using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class SkinPicker : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject skinPreviewPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] private float itemSpacing;
    [SerializeField] private float snapSpeed = 5f;
    [SerializeField] public List<string> skinNames = new();
    [SerializeField] private List<RectTransform> skinItemRects = new();

    [Header("Scale Effect")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float unselectedScale = 0.5f;
    [SerializeField] private float scaleSmoothing = 3f;

    private float _contentStartPosition;
    private int _currentIndex;
    private float _itemWidth;
    private bool _isSnapping;
    private float _snapTargetPosition;

    private void Start()
    {
        if (scrollRect == null || content == null || skinPreviewPrefab == null || leftButton == null || rightButton == null)
        {
            Debug.LogError("Please assign all UI references in the inspector!");
            return;
        }

        InitializeSkinPreviews();
        SetupButtons();
        UpdateContentPosition();
    }

    private void InitializeSkinPreviews()
    {
        var currentX = 0f;
        for (var i = 0; i < skinNames.Count; i++)
        {
            var previewGo = Instantiate(skinPreviewPrefab, content);
            var itemRect = previewGo.GetComponent<RectTransform>();
            skinItemRects.Add(itemRect);

            var previewAnimator = previewGo.GetComponent<SkinPreviewAnimator>();
            if (previewAnimator != null)
                previewAnimator.SetSkinName(skinNames[i]);
            else
                Debug.LogWarning("SkinPreviewPrefab is missing SkinPreviewAnimator script!");

            itemRect.anchoredPosition = new Vector2(currentX, 0f);
            if (i == 0)
                _itemWidth = itemRect.rect.width;

            currentX += _itemWidth + itemSpacing;
        }

        content.sizeDelta = skinNames.Count > 0
            ? new Vector2((_itemWidth + itemSpacing) * skinNames.Count - itemSpacing, content.sizeDelta.y)
            : new Vector2(0, content.sizeDelta.y);

        _contentStartPosition = content.anchoredPosition.x;
    }

    private void SetupButtons()
    {
        leftButton.onClick.AddListener(ScrollLeft);
        rightButton.onClick.AddListener(ScrollRight);
        UpdateButtonStates();
    }

    private void ScrollLeft()
    {
        _currentIndex--;
        if (_currentIndex < 0)
            _currentIndex = 0;
        UpdateContentPosition();
        UpdateButtonStates();
    }

    private void ScrollRight()
    {
        _currentIndex++;
        if (_currentIndex >= skinNames.Count)
            _currentIndex = skinNames.Count - 1;
        UpdateContentPosition();
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        leftButton.interactable = _currentIndex > 0;
        rightButton.interactable = _currentIndex < skinNames.Count - 1;
    }

    private void UpdateContentPosition()
    {
        _snapTargetPosition = _contentStartPosition - (_itemWidth + itemSpacing) * _currentIndex;
        _isSnapping = true;
        SaveSelectedSkin();
    }

    private void Update()
    {
        if (_isSnapping)
        {
            var anchoredPosition = content.anchoredPosition;
            content.anchoredPosition = Vector2.Lerp(
                anchoredPosition,
                new Vector2(_snapTargetPosition, anchoredPosition.y),
                snapSpeed * Time.deltaTime
            );

            if (Mathf.Abs(content.anchoredPosition.x - _snapTargetPosition) < 0.01f)
            {
                content.anchoredPosition = new Vector2(_snapTargetPosition, content.anchoredPosition.y);
                _isSnapping = false;
            }
        }

        UpdateItemScales();
    }

    private void UpdateItemScales()
    {
        if (skinItemRects.Count == 0)
            return;

        for (var i = 0; i < skinItemRects.Count; i++)
        {
            float distanceFactor = Mathf.Abs(i - _currentIndex);

            var targetScale = Mathf.Lerp(selectedScale, unselectedScale, Mathf.Clamp01(distanceFactor));

            skinItemRects[i].localScale = Vector3.Lerp(
                skinItemRects[i].localScale,
                new Vector3(targetScale, targetScale, 1f),
                scaleSmoothing * Time.deltaTime
            );
        }
    }

    private void SaveSelectedSkin()
    {
        if (_currentIndex < 0 || _currentIndex >= skinNames.Count)
            return;

        var selectedSkin = skinNames[_currentIndex];
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Skin", selectedSkin } });
    }
}
