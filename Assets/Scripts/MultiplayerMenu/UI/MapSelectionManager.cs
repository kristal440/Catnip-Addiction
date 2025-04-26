using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectionManager : MonoBehaviour
{
    [SerializeField] private ScrollListController visualController;
    [SerializeField] private ScrollListSelectionHandler selectionHandler;
    [SerializeField] private Transform mapsContainer;

    [Header("Map Preview")]
    [SerializeField] private Image mapPreviewImage;
    [SerializeField] private List<MapPreviewData> mapPreviews = new();

    [Header("Settings")]
    [SerializeField] private bool skipMapsNotInBuild = true;
    [Tooltip("When enabled, maps not included in the build settings will be skipped")]
    public delegate void MapSelectedHandler(string mapSceneName, string mapDisplayName);
    public event MapSelectedHandler OnMapSelected;

    private readonly Dictionary<string, string> _availableMaps = new();
    private string _selectedMapName;

    [Serializable]
    public class MapPreviewData
    {
        public string mapSceneName;
        public Sprite previewImage;
    }

    private void Awake()
    {
        _selectedMapName = "GameScene_Map1_Multi";
    }

    private void OnEnable()
    {
        if (selectionHandler != null)
            selectionHandler.OnItemSelected += HandleMapSelection;
    }

    private void OnDisable()
    {
        if (selectionHandler != null)
            selectionHandler.OnItemSelected -= HandleMapSelection;
    }

    internal void Initialize(string defaultMapName = "GameScene_Map1_Multi")
    {
        _selectedMapName = defaultMapName;
        LoadAvailableMaps();
        CreateMapSelectionButtons();
    }

    private void LoadAvailableMaps()
    {
        _availableMaps.Clear();

        foreach (Transform child in mapsContainer)
        {
            if (!child.gameObject.activeSelf) continue;

            var sceneName = child.gameObject.name;

            var sceneInBuild = false;
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneNameFromBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (sceneNameFromBuild != sceneName) continue;

                sceneInBuild = true;
                break;
            }

            if (!sceneInBuild && skipMapsNotInBuild)
            {
                Debug.LogWarning($"Skipping map '{sceneName}' because it's not included in the build profile.");
                continue;
            }

            var displayName = sceneName;
            var displayText = child.GetComponentInChildren<TextMeshProUGUI>();
            if (displayText)
                displayName = displayText.text;

            _availableMaps.Add(sceneName, displayName);
        }

        if (!_availableMaps.ContainsKey(_selectedMapName) && _availableMaps.Count > 0)
            _selectedMapName = _availableMaps.Keys.First();
    }

    private void CreateMapSelectionButtons()
    {
        var existingButtons = (from Transform child in mapsContainer select child.gameObject).ToList();
        var buttonIndex = 0;

        foreach (var (_, displayName) in _availableMaps)
        {
            if (buttonIndex >= existingButtons.Count)
                break;

            var mapButton = existingButtons[buttonIndex];
            mapButton.SetActive(true);

            var mapNameText = mapButton.transform.Find("MapNameTxt").GetComponent<TextMeshProUGUI>();
            if (mapNameText)
                mapNameText.text = displayName;

            buttonIndex++;
        }

        for (var i = buttonIndex; i < existingButtons.Count; i++)
            existingButtons[i].SetActive(false);

        visualController.InitializeItems();
        selectionHandler.Initialize();

        SelectMap(_selectedMapName);
    }

    private void HandleMapSelection(int index, GameObject selectedObject)
    {
        if (index < 0 || index >= _availableMaps.Count) return;

        _selectedMapName = _availableMaps.Keys.ElementAt(index);
        OnMapSelected?.Invoke(_selectedMapName, _availableMaps[_selectedMapName]);

        UpdateMapPreviewImage(_selectedMapName);
    }

    private void SelectMap(string mapSceneName)
    {
        if (!_availableMaps.ContainsKey(mapSceneName)) return;

        var index = _availableMaps.Keys.ToList().IndexOf(mapSceneName);
        if (index >= 0)
            selectionHandler.SelectItemProgrammatically(index);

        UpdateMapPreviewImage(mapSceneName);
    }

    private void UpdateMapPreviewImage(string mapName)
    {
        if (mapPreviewImage == null) return;

        var previewData = mapPreviews.FirstOrDefault(p => p.mapSceneName == mapName);

        if (previewData != null && previewData.previewImage != null)
        {
            mapPreviewImage.sprite = previewData.previewImage;
            mapPreviewImage.enabled = true;
        }
        else
        {
            Debug.LogWarning($"No preview image found for map '{mapName}'");
        }
    }

    public string GetSelectedMapName()
    {
        return _selectedMapName;
    }
}
