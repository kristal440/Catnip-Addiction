using System.Collections.Generic;
using UnityEngine;

public class MenuCatController : MonoBehaviour
{
    [Header("Cat Animation")]
    public GameObject catPrefab;
    [Range(1f, 10f)]
    public float catSpawnInterval = 5f;
    public float catSpawnIntervalVariance = 1.5f;
    [Range(2f, 6f)]
    public float slowCatSpeed = 3.5f;
    [Range(4f, 8f)]
    public float fastCatSpeed = 7.8f;
    [Range(1f, 2f)]
    public float animationSpeedMultiplier = 1.5f;
    public float animationSpeedVariance = 0.2f;
    public float spawnYPosition = -2f;
    public float spawnYVariance = 0.5f;
    public float destroyXPosition = 12f;

    [Header("Animation States")]
    [Tooltip("Available skin variants (corresponds to the 'Skin' parameter on Animator)")]
    public int[] skinAnimVariables = { 0, 1, 2 };

    [Header("Optimization")]
    public int poolSize = 10;

    [Header("Background Controller Reference")]
    public MenuBackgroundController backgroundController;

    private float _timeSinceLastSpawn;
    private float _nextSpawnTime;
    private Camera _camera;
    private Queue<GameObject> _inactiveCats;
    private float _spawnXPosition;

    private void Awake()
    {
        _camera = Camera.main;

        if (backgroundController == null)
            backgroundController = GetComponent<MenuBackgroundController>();
    }

    private void Start()
    {
        if (catPrefab == null)
        {
            Debug.LogError("No cat prefab assigned to MenuCatController.");
            return;
        }

        InitializeCatPool();
        CalculateSpawnPosition();

        _nextSpawnTime = Random.Range(
            catSpawnInterval - catSpawnIntervalVariance,
            catSpawnInterval + catSpawnIntervalVariance);
    }

    private void InitializeCatPool()
    {
        _inactiveCats = new Queue<GameObject>(poolSize);

        for (var i = 0; i < poolSize; i++)
        {
            var cat = Instantiate(catPrefab, Vector3.one * -100f, Quaternion.identity, transform);
            _inactiveCats.Enqueue(cat);
            cat.SetActive(false);
        }
    }

    private void CalculateSpawnPosition()
    {
        if (_camera != null)
            _spawnXPosition = _camera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x - 2f;
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
        if (_inactiveCats.Count == 0)
            return;

        var cat = _inactiveCats.Dequeue();
        cat.SetActive(true);

        var yPos = spawnYPosition + Random.Range(-spawnYVariance, spawnYVariance);
        cat.transform.position = new Vector3(_spawnXPosition, yPos, 0f);

        var moveSpeed = Random.value < 0.5f ? slowCatSpeed : fastCatSpeed;

        var baseAnimSpeed = moveSpeed * animationSpeedMultiplier;
        var minAnimSpeed = Mathf.Max(1f, baseAnimSpeed - animationSpeedVariance);
        var maxAnimSpeed = baseAnimSpeed + animationSpeedVariance;

        var skinIndex = Random.Range(0, skinAnimVariables.Length);
        var skinVariant = skinAnimVariables[skinIndex];

        var catRunner = cat.GetComponent<MenuCatRunner>();
        catRunner.Initialize(
            moveSpeed,
            destroyXPosition,
            skinVariant,
            minAnimSpeed,
            maxAnimSpeed
        );

        StartCoroutine(WaitForCatDeactivation(cat));
    }

    private System.Collections.IEnumerator WaitForCatDeactivation(GameObject cat)
    {
        yield return new WaitUntil(() => !cat.activeInHierarchy);
        _inactiveCats.Enqueue(cat);
    }

    public void TransitionToMenu(string menuName)
    {
        if (backgroundController != null)
            backgroundController.TransitionToMenu(menuName);
    }
}
