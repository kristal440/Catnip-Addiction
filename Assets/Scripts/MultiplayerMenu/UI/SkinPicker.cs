using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a horizontal skin selection UI with scrolling, item scaling, and Photon network synchronization.
/// </summary>
/// <inheritdoc />
public class SkinPicker : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] [Tooltip("Button to navigate to the previous skin")] private Button leftButton;
    [SerializeField] [Tooltip("Button to navigate to the next skin")] private Button rightButton;
    [SerializeField] [Tooltip("Container for skin preview items")] private RectTransform content;
    [SerializeField] [Tooltip("Prefab used to display each skin preview")] private GameObject skinPreviewPrefab;
    [SerializeField] [Tooltip("ScrollRect component for scrolling through skins")] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] [Tooltip("Space between each skin preview item")] private float itemSpacing;
    [SerializeField] [Tooltip("Speed at which the view snaps to selected skin")] private float snapSpeed = 5f;
    [SerializeField] [Tooltip("List of available skin names")] public List<string> skinNames = new();
    [SerializeField] [Tooltip("References to skin item rect transforms")] private List<RectTransform> skinItemRects = new();

    [Header("Scale Effect")]
    [SerializeField] [Tooltip("Scale factor for the selected skin")] private float selectedScale = 1.2f;
    [SerializeField] [Tooltip("Scale factor for unselected skins")] private float unselectedScale = 0.5f;
    [SerializeField] [Tooltip("How quickly items scale up/down when selected/deselected")] private float scaleSmoothing = 3f;

    private float _contentStartPosition;
    private int _currentIndex;
    private float _itemWidth;
    private bool _isSnapping;
    private float _snapTargetPosition;

    // Sets up skin previews and UI controls on startup
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

    // Creates skin preview objects and positions them in the scroll view
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

    // Configures navigation button click handlers
    private void SetupButtons()
    {
        leftButton.onClick.AddListener(ScrollLeft);
        rightButton.onClick.AddListener(ScrollRight);
        UpdateButtonStates();
    }

    // Navigates to the previous skin
    private void ScrollLeft()
    {
        _currentIndex--;
        if (_currentIndex < 0)
            _currentIndex = 0;
        UpdateContentPosition();
        UpdateButtonStates();
    }

    // Navigates to the next skin
    private void ScrollRight()
    {
        _currentIndex++;
        if (_currentIndex >= skinNames.Count)
            _currentIndex = skinNames.Count - 1;
        UpdateContentPosition();
        UpdateButtonStates();
    }

    // Enables/disables buttons based on current selection position
    private void UpdateButtonStates()
    {
        leftButton.interactable = _currentIndex > 0;
        rightButton.interactable = _currentIndex < skinNames.Count - 1;
    }

    // Updates content position to center the selected skin
    private void UpdateContentPosition()
    {
        _snapTargetPosition = _contentStartPosition - (_itemWidth + itemSpacing) * _currentIndex;
        _isSnapping = true;
        SaveSelectedSkin();
    }

    // Handles smooth scrolling and item scaling each frame
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

    // Applies scale effects based on distance from center selection
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

    // Syncs selected skin with Photon network
    private void SaveSelectedSkin()
    {
        if (_currentIndex < 0 || _currentIndex >= skinNames.Count)
            return;

        var selectedSkin = skinNames[_currentIndex];
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Skin", selectedSkin } });
    }
}
