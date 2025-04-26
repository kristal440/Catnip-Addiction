using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages cloud sprites in a parallax background system that responds to player movement
/// </summary>
public class CloudManager : MonoBehaviour
{
    [Header("Cloud Settings")]
    [SerializeField] [Tooltip("Collection of cloud sprite variations to use")] private List<Sprite> cloudSprites;
    [SerializeField] [Tooltip("Total number of clouds to generate")] private int numberOfClouds = 15;
    [SerializeField] [Tooltip("Parent transform for all generated clouds")] private Transform parentTransform;

    [Header("Movement Settings")]
    [SerializeField] [Tooltip("Minimum movement speed for clouds")] private float minSpeed = 0.1f;
    [SerializeField] [Tooltip("Maximum movement speed for clouds")] private float maxSpeed = 0.5f;
    [SerializeField] [Tooltip("Direction clouds move (true = right, false = left)")] private bool moveRight = true;
    [SerializeField] [Tooltip("Factor affecting how clouds react to player movement")] private float parallaxFactor;

    [Header("Position Settings")]
    [SerializeField] [Tooltip("Minimum Y position for cloud placement")] private float minY = -5f;
    [SerializeField] [Tooltip("Maximum Y position for cloud placement")] private float maxY = 20f;
    [SerializeField] [Tooltip("Minimum scale multiplier for clouds")] private float minScale = 3f;
    [SerializeField] [Tooltip("Maximum scale multiplier for clouds")] private float maxScale = 4f;
    [SerializeField] [Tooltip("Width across which clouds are distributed")] private float distributionWidth = 100f;

    [Header("Advanced")]
    [SerializeField] [Tooltip("Sorting layer name for cloud sprites")] private string sortingLayerName = "Background";
    [SerializeField] [Tooltip("Base sorting order for cloud sprites")] private int baseSortingOrder = -6;
    [SerializeField] [Tooltip("Tag used to identify player object")] private string playerTag = "Player";

    private readonly List<CloudData> _clouds = new();
    private Camera _mainCamera;
    private float _screenWidthInUnits;
    private Transform _playerTransform;
    private Vector3 _lastPlayerPosition;
    private float _playerMovementDirection;

    private sealed class CloudData
    {
        public GameObject GameObject;
        public float Speed;
    }

    // Initializes camera, parent transform, player reference, and creates initial clouds
    private void Start()
    {
        _mainCamera = Camera.main;
        if (parentTransform == null)
            parentTransform = transform;

        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            _playerTransform = player.transform;
            _lastPlayerPosition = _playerTransform.position;
        }
        else
        {
            Debug.LogWarning("Player not found! Make sure to tag your player with '" + playerTag + "'.");
        }

        if (_mainCamera != null) _screenWidthInUnits = _mainCamera.orthographicSize * 2f * _mainCamera.aspect;

        InitializeClouds();
    }

    // Updates player movement direction and cloud positions each frame
    private void Update()
    {
        UpdatePlayerMovementDirection();
        UpdateClouds();
    }

    // Calculates and stores player movement direction based on position change
    private void UpdatePlayerMovementDirection()
    {
        if (!_playerTransform) return;

        var currentPlayerPosition = _playerTransform.position;
        var deltaX = currentPlayerPosition.x - _lastPlayerPosition.x;

        if (Mathf.Abs(deltaX) > 0.01f)
        {
            _playerMovementDirection = Mathf.Sign(deltaX);

            moveRight = _playerMovementDirection < 0f;
        }

        _lastPlayerPosition = currentPlayerPosition;
    }

    // Creates the initial set of cloud objects
    private void InitializeClouds()
    {
        if (cloudSprites == null || cloudSprites.Count == 0)
        {
            Debug.LogError("No cloud sprites assigned to CloudManager!");
            return;
        }

        for (var i = 0; i < numberOfClouds; i++)
            CreateCloud(i);
    }

    // Creates a single cloud with randomized properties
    private void CreateCloud(int index = -1)
    {
        var cloudObject = new GameObject("Cloud");
        var spriteRenderer = cloudObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = cloudSprites[Random.Range(0, cloudSprites.Count)];

        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = baseSortingOrder;

        cloudObject.transform.SetParent(parentTransform);

        var cloud = new CloudData
        {
            GameObject = cloudObject,
            Speed = Random.Range(minSpeed, maxSpeed)
        };

        var scale = Random.Range(minScale, maxScale);
        cloudObject.transform.localScale = new Vector3(scale, scale, 1f);

        if (index >= 0)
            PositionCloudEvenly(cloud, index);
        else
            PositionCloudRandomly(cloud);

        _clouds.Add(cloud);
    }

    // Positions clouds evenly across the distribution width during initialization
    private void PositionCloudEvenly(CloudData cloud, int index)
    {
        var cameraCenterX = _mainCamera.transform.position.x;

        var segmentWidth = distributionWidth / numberOfClouds;

        var segmentStart = cameraCenterX - (distributionWidth / 2f) + (index * segmentWidth);
        var xPos = segmentStart + Random.Range(0, segmentWidth);
        var yPos = Random.Range(minY, maxY);

        cloud.GameObject.transform.position = new Vector3(xPos, yPos, 0);
    }

    // Positions a cloud randomly outside the screen bounds for recycling
    private void PositionCloudRandomly(CloudData cloud)
    {
        float xPos;
        var yPos = Random.Range(minY, maxY);

        var position = _mainCamera.transform.position;
        var leftBound = position.x - (_screenWidthInUnits / 2f);
        var rightBound = position.x + (_screenWidthInUnits / 2f);

        var offset = Random.Range(1f, 3f);

        if (moveRight)
            xPos = leftBound - offset;
        else
            xPos = rightBound + offset;

        cloud.GameObject.transform.position = new Vector3(xPos, yPos, 0);
    }

    // Moves clouds and recycles them when they go off-screen
    private void UpdateClouds()
    {
        var movementDirection = moveRight ? 1f : -1f;

        var speedMultiplier = 1f;
        if (_playerTransform && Mathf.Abs(_playerMovementDirection) > 0.01f)
            speedMultiplier = 1f + (Mathf.Abs(_playerMovementDirection) * parallaxFactor);

        var position = _mainCamera.transform.position;
        var leftBound = position.x - (_screenWidthInUnits / 2f) - 2f;
        var rightBound = position.x + (_screenWidthInUnits / 2f) + 2f;

        foreach (var cloud in _clouds.Where(static cloud => cloud.GameObject.activeInHierarchy))
        {
            cloud.GameObject.transform.Translate(Vector3.right * (movementDirection * cloud.Speed * speedMultiplier * Time.deltaTime));

            var cloudPos = cloud.GameObject.transform.position;

            var isOutsideLeft = cloudPos.x < leftBound;
            var isOutsideRight = cloudPos.x > rightBound;

            if (!isOutsideLeft && !isOutsideRight) continue;

            switch (moveRight)
            {
                case true when isOutsideRight:
                    cloud.GameObject.transform.position = new Vector3(
                        leftBound,
                        Random.Range(minY, maxY),
                        0
                    );
                    break;
                case false when isOutsideLeft:
                    cloud.GameObject.transform.position = new Vector3(
                        rightBound,
                        Random.Range(minY, maxY),
                        0
                    );
                    break;
                default:
                    if (moveRight)
                        cloud.GameObject.transform.position = new Vector3(
                            leftBound - Random.Range(0.5f, 2f),
                            Random.Range(minY, maxY),
                            0
                        );
                    else
                        cloud.GameObject.transform.position = new Vector3(
                            rightBound + Random.Range(0.5f, 2f),
                            Random.Range(minY, maxY),
                            0
                        );
                    break;
            }

            RandomizeCloudProperties(cloud);
        }
    }

    // Applies new random speed, scale, and sprite to a cloud
    private void RandomizeCloudProperties(CloudData cloud)
    {
        cloud.Speed = Random.Range(minSpeed, maxSpeed);

        var scale = Random.Range(minScale, maxScale);
        cloud.GameObject.transform.localScale = new Vector3(scale, scale, 1f);

        var spriteRenderer = cloud.GameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer)
            spriteRenderer.sprite = cloudSprites[Random.Range(0, cloudSprites.Count)];
    }
}
