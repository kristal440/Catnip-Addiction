using System.Collections.Generic;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Controls the spawning and behavior of cats in the menu background.
/// </summary>
public class MenuCatController : MonoBehaviour
{
    [Header("Cat Animation")]
    [SerializeField] [Tooltip("Prefab used to spawn cats in the menu")] private GameObject catPrefab;
    [SerializeField] [Range(1f, 10f)] [Tooltip("Base time between cat spawns in seconds")] private float catSpawnInterval = 5f;
    [SerializeField] [Tooltip("Random variation applied to spawn interval")] private float catSpawnIntervalVariance = 1.5f;
    [SerializeField] [Range(2f, 6f)] [Tooltip("Movement speed for slow cats")] private float slowCatSpeed = 3.5f;
    [SerializeField] [Range(4f, 8f)] [Tooltip("Movement speed for fast cats")] private float fastCatSpeed = 7.8f;
    [SerializeField] [Range(1f, 2f)] [Tooltip("Base multiplier for animation speed")] private float animationSpeedMultiplier = 1.5f;
    [SerializeField] [Tooltip("Random variation applied to animation speed")] private float animationSpeedVariance = 0.2f;
    [SerializeField] [Tooltip("Base Y position for spawning cats")] private float spawnYPosition = -2f;
    [SerializeField] [Tooltip("Random variation applied to spawn Y position")] private float spawnYVariance = 0.5f;
    [SerializeField] [Tooltip("X position at which cats are destroyed")] private float destroyXPosition = 12f;

    [Header("Animation States")]
    [SerializeField] [Tooltip("Available skin variants (corresponds to the 'Skin' parameter on Animator)")] private int[] skinAnimVariables = { 0, 1, 2 };

    [Header("Optimization")]
    [SerializeField] [Tooltip("Maximum number of cat objects to create for object pooling")] private int poolSize = 10;

    [Header("Background Controller Reference")]
    [SerializeField] [Tooltip("Reference to the menu background controller")] private MenuBackgroundController backgroundController;

    private float _timeSinceLastSpawn;
    private float _nextSpawnTime;
    private Camera _camera;
    private Queue<GameObject> _inactiveCats;
    private float _spawnXPosition;

    // Initialize components and references
    private void Awake()
    {
        _camera = Camera.main;

        if (backgroundController == null)
            backgroundController = GetComponent<MenuBackgroundController>();
    }

    // Setup cat pool and initial spawn parameters
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

    // Create pool of reusable cat objects
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

    // Determine the X position where cats will spawn off-screen
    private void CalculateSpawnPosition()
    {
        if (_camera != null)
            _spawnXPosition = _camera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x - 2f;
    }

    // Check spawn timer and trigger cat spawning
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

    // Take a cat from the pool and set it up to run across the screen
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

    // Return the cat to the pool once it's deactivated
    private System.Collections.IEnumerator WaitForCatDeactivation(GameObject cat)
    {
        yield return new WaitUntil(() => !cat.activeInHierarchy);

        _inactiveCats.Enqueue(cat);
    }

    // Request a menu transition from the background controller
    public void TransitionToMenu(string menuName)
    {
        if (backgroundController != null)
            backgroundController.TransitionToMenu(menuName);
    }
}
