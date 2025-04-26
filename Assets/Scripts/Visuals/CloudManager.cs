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

    [Header("Visual Settings")]
    [SerializeField] [Tooltip("Minimum alpha/transparency for clouds")] private float minAlpha = 0.8f;
    [SerializeField] [Tooltip("Maximum alpha/transparency for clouds")] private float maxAlpha = 1.0f;
    [SerializeField] [Tooltip("Color tint to apply to clouds")] private Color cloudTint = Color.white;
    [SerializeField] [Tooltip("Whether to apply random color variations")] private bool useRandomColorVariation;
    [SerializeField] [Tooltip("Minimum color variation (RGB)")] private float minColorVariation = 0.8f;
    [SerializeField] [Tooltip("Maximum color variation (RGB)")] private float maxColorVariation = 1.0f;
    [SerializeField] [Tooltip("Chance for a cloud to be flipped horizontally")] private float horizontalFlipChance;
    [SerializeField] [Tooltip("Chance for a cloud to be flipped vertically")] private float verticalFlipChance;
    [SerializeField] [Tooltip("Whether to apply random rotation to clouds")] private bool useRandomRotation;
    [SerializeField] [Tooltip("Minimum rotation angle (degrees)")] private float minRotation = -15f;
    [SerializeField] [Tooltip("Maximum rotation angle (degrees)")] private float maxRotation = 15f;

    [Header("Shadow Settings")]
    [SerializeField] [Tooltip("Whether to add shadows to clouds")] private bool useShadows = true;
    [SerializeField] [Tooltip("Shadow color")] private Color shadowColor = new(0, 0, 0, 0.3f);
    [SerializeField] [Tooltip("Shadow offset X")] private float shadowOffsetX = 0.025f;
    [SerializeField] [Tooltip("Shadow offset Y")] private float shadowOffsetY = -0.025f;
    [SerializeField] [Tooltip("Shadow sorting order offset from cloud")] private int shadowSortingOrderOffset = -1;

    [Header("Movement Settings")]
    [SerializeField] [Tooltip("Minimum movement speed for clouds")] private float minSpeed = 0.1f;
    [SerializeField] [Tooltip("Maximum movement speed for clouds")] private float maxSpeed = 0.5f;
    [SerializeField] [Tooltip("Direction clouds move (true = right, false = left)")] private bool moveRight = true;
    [SerializeField] [Tooltip("Factor affecting how clouds react to player movement")] private float parallaxFactor = 0.5f;

    [Header("Position Settings")]
    [SerializeField] [Tooltip("Minimum Y position for cloud placement")] private float minY = -5f;
    [SerializeField] [Tooltip("Maximum Y position for cloud placement")] private float maxY = 20f;
    [SerializeField] [Tooltip("Minimum scale multiplier for clouds")] private float minScale = 3f;
    [SerializeField] [Tooltip("Maximum scale multiplier for clouds")] private float maxScale = 4f;
    [SerializeField] [Tooltip("Width across which clouds are distributed")] private float distributionWidth = 150f;

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
        public GameObject ShadowObject;
        public float Speed;
        public float Alpha;
        public Color TintColor;
        public bool FlippedHorizontally;
        public bool FlippedVertically;
        public float Rotation;
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
            Speed = Random.Range(minSpeed, maxSpeed),
            Alpha = Random.Range(minAlpha, maxAlpha),
            TintColor = GetRandomTintColor(),
            FlippedHorizontally = Random.value < horizontalFlipChance,
            FlippedVertically = Random.value < verticalFlipChance,
            Rotation = useRandomRotation ? Random.Range(minRotation, maxRotation) : 0f
        };

        var scale = Random.Range(minScale, maxScale);
        cloudObject.transform.localScale = new Vector3(
            cloud.FlippedHorizontally ? -scale : scale,
            cloud.FlippedVertically ? -scale : scale,
            1f);

        if (useRandomRotation)
            cloudObject.transform.rotation = Quaternion.Euler(0, 0, cloud.Rotation);

        ApplyVisualSettings(cloud);

        if (useShadows)
            CreateShadowForCloud(cloud, spriteRenderer.sprite);

        if (index >= 0)
            PositionCloudEvenly(cloud, index);
        else
            PositionCloudRandomly(cloud);

        _clouds.Add(cloud);
    }

    // Creates a shadow object for a cloud
    private void CreateShadowForCloud(CloudData cloud, Sprite cloudSprite)
    {
        var shadowObject = new GameObject("Cloud_Shadow");
        shadowObject.transform.SetParent(cloud.GameObject.transform);

        var shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = cloudSprite;
        shadowRenderer.sortingLayerName = sortingLayerName;
        shadowRenderer.sortingOrder = baseSortingOrder + shadowSortingOrderOffset;
        shadowRenderer.color = shadowColor;

        shadowObject.transform.localPosition = new Vector3(shadowOffsetX, shadowOffsetY, 0);
        shadowObject.transform.localScale = Vector3.one;
        shadowObject.transform.localRotation = Quaternion.identity;

        cloud.ShadowObject = shadowObject;
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

    // Applies visual settings to a cloud
    private static void ApplyVisualSettings(CloudData cloud)
    {
        var spriteRenderer = cloud.GameObject.GetComponent<SpriteRenderer>();
        if (!spriteRenderer) return;

        var finalColor = cloud.TintColor;
        finalColor.a = cloud.Alpha;
        spriteRenderer.color = finalColor;
    }

    // Gets a random color tint based on settings
    private Color GetRandomTintColor()
    {
        if (!useRandomColorVariation)
            return cloudTint;

        return new Color(
            cloudTint.r * Random.Range(minColorVariation, maxColorVariation),
            cloudTint.g * Random.Range(minColorVariation, maxColorVariation),
            cloudTint.b * Random.Range(minColorVariation, maxColorVariation),
            cloudTint.a
        );
    }

    // Applies new random speed, scale, and sprite to a cloud
    private void RandomizeCloudProperties(CloudData cloud)
    {
        cloud.Speed = Random.Range(minSpeed, maxSpeed);
        cloud.Alpha = Random.Range(minAlpha, maxAlpha);
        cloud.TintColor = GetRandomTintColor();
        cloud.FlippedHorizontally = Random.value < horizontalFlipChance;
        cloud.FlippedVertically = Random.value < verticalFlipChance;
        cloud.Rotation = useRandomRotation ? Random.Range(minRotation, maxRotation) : 0f;

        var scale = Random.Range(minScale, maxScale);
        cloud.GameObject.transform.localScale = new Vector3(
            cloud.FlippedHorizontally ? -scale : scale,
            cloud.FlippedVertically ? -scale : scale,
            1f);

        if (useRandomRotation)
            cloud.GameObject.transform.rotation = Quaternion.Euler(0, 0, cloud.Rotation);

        var spriteRenderer = cloud.GameObject.GetComponent<SpriteRenderer>();
        if (!spriteRenderer) return;

        var newSprite = cloudSprites[Random.Range(0, cloudSprites.Count)];
        spriteRenderer.sprite = newSprite;
        ApplyVisualSettings(cloud);

        if (!useShadows || !cloud.ShadowObject) return;

        var shadowRenderer = cloud.ShadowObject.GetComponent<SpriteRenderer>();
        if (shadowRenderer)
            shadowRenderer.sprite = newSprite;
    }
}
