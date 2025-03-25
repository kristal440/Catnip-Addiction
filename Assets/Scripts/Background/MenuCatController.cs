using System.Collections.Generic;
using UnityEngine;

public class MenuCatController : MonoBehaviour
{
    [Header("Cat Animation")]
    public GameObject catPrefab;
    [Tooltip("Available skin variants (corresponds to the 'Skin' parameter on Animator)")]
    public int[] skinVariants = { 0, 1, 2 };
    [Range(1f, 10f)]
    public float catSpawnInterval = 5f;
    public float catSpawnIntervalVariance = 1.5f;
    [Range(2f, 6f)]
    public float slowCatSpeed = 3f;
    [Range(4f, 8f)]
    public float fastCatSpeed = 6f;
    [Range(1f, 2f)]
    public float animationSpeedMultiplier = 1.5f;
    public float animationSpeedVariance = 0.2f;
    public float spawnYPosition = -2f;
    public float spawnYVariance = 0.5f;
    public float destroyXPosition = 12f;

    [Header("Optimization")]
    public int poolSize = 10;

    [Header("Background Controller Reference")]
    public MenuBackgroundController backgroundController;

    private float _timeSinceLastSpawn;
    private float _nextSpawnTime;
    private Camera _camera;
    private List<GameObject> _catPool;
    private int _currentCatIndex;

    private void Awake()
    {
        _camera = Camera.main;

        if (backgroundController == null)
        {
            backgroundController = GetComponent<MenuBackgroundController>();
        }
    }

    private void Start()
    {
        if (catPrefab == null)
        {
            Debug.LogError("No cat prefab assigned to MenuCatController.");
            return;
        }

        InitializeCatPool();

        _nextSpawnTime = Random.Range(
            catSpawnInterval - catSpawnIntervalVariance,
            catSpawnInterval + catSpawnIntervalVariance);
    }

    private void InitializeCatPool()
    {
        _catPool = new List<GameObject>(poolSize);

        for (var i = 0; i < poolSize; i++)
        {
            var cat = Instantiate(catPrefab, Vector3.one * -100f, Quaternion.identity, transform);
            _catPool.Add(cat);
            cat.SetActive(false);
        }
    }

    private void Update()
    {
        _timeSinceLastSpawn += Time.deltaTime;
        if (!(_timeSinceLastSpawn >= _nextSpawnTime)) return;
        SpawnCat();
        _timeSinceLastSpawn = 0f;
        _nextSpawnTime = Random.Range(
            catSpawnInterval - catSpawnIntervalVariance,
            catSpawnInterval + catSpawnIntervalVariance);
    }

    private void SpawnCat()
    {
        if (_catPool == null || _catPool.Count == 0)
            return;

        if (!_camera) return;

        GameObject cat = null;
        var attempts = 0;

        while (attempts < _catPool.Count)
        {
            _currentCatIndex = (_currentCatIndex + 1) % _catPool.Count;
            if (!_catPool[_currentCatIndex].activeInHierarchy)
            {
                cat = _catPool[_currentCatIndex];
                break;
            }
            attempts++;
        }

        if (!cat)
            return;

        var spawnXPosition = _camera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x - 2f;
        var yPos = spawnYPosition + Random.Range(-spawnYVariance, spawnYVariance);
        cat.transform.position = new Vector3(spawnXPosition, yPos, 0f);

        cat.SetActive(true);

        var moveSpeed = Random.value < 0.5f ? slowCatSpeed : fastCatSpeed;

        var baseAnimSpeed = moveSpeed * animationSpeedMultiplier;
        var minAnimSpeed = Mathf.Max(1f, baseAnimSpeed - animationSpeedVariance);
        var maxAnimSpeed = baseAnimSpeed + animationSpeedVariance;

        var skinVariant = skinVariants[Random.Range(0, skinVariants.Length)];
        cat.GetComponent<MenuCatRunner>().Initialize(moveSpeed, destroyXPosition, skinVariant, minAnimSpeed, maxAnimSpeed);
    }

    public void TransitionToMenu(string menuName)
    {
        if (backgroundController != null)
        {
            backgroundController.TransitionToMenu(menuName);
        }
    }
}
