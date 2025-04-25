using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CloudManager : MonoBehaviour
{
    [Header("Cloud Settings")]
    [SerializeField] private List<Sprite> cloudSprites;
    [SerializeField] private int numberOfClouds = 15;
    [SerializeField] private Transform parentTransform;

    [Header("Movement Settings")]
    [SerializeField] private float minSpeed = 0.1f;
    [SerializeField] private float maxSpeed = 0.5f;
    [SerializeField] private bool moveRight = true;
    [SerializeField] private float parallaxFactor;

    [Header("Position Settings")]
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 20f;
    [SerializeField] private float minScale = 3f;
    [SerializeField] private float maxScale = 4f;

    [Header("Advanced")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int baseSortingOrder = -6;
    [SerializeField] private string playerTag = "Player";

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

    private void Update()
    {
        UpdatePlayerMovementDirection();
        UpdateClouds();
    }

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

    private void InitializeClouds()
    {
        if (cloudSprites == null || cloudSprites.Count == 0)
        {
            Debug.LogError("No cloud sprites assigned to CloudManager!");
            return;
        }

        for (var i = 0; i < numberOfClouds; i++)
            CreateCloud();
    }

    private void CreateCloud()
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

        PositionCloudRandomly(cloud);

        _clouds.Add(cloud);
    }

    private void PositionCloudRandomly(CloudData cloud)
    {
        float xPos;
        var yPos = Random.Range(minY, maxY);

        var position = _mainCamera.transform.position;
        var leftBound = position.x - (_screenWidthInUnits / 2f);
        var rightBound = position.x + (_screenWidthInUnits / 2f);

        if (Random.value > 0.3f)
        {
            xPos = Random.Range(leftBound, rightBound);
        }
        else
        {
            var offset = Random.Range(1f, 3f);

            if (moveRight)
                xPos = leftBound - offset;
            else
                xPos = rightBound + offset;
        }

        cloud.GameObject.transform.position = new Vector3(xPos, yPos, 0);
    }

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
                    RandomizeCloudProperties(cloud);
                    break;
                case false when isOutsideLeft:
                    cloud.GameObject.transform.position = new Vector3(
                        rightBound,
                        Random.Range(minY, maxY),
                        0
                    );
                    RandomizeCloudProperties(cloud);
                    break;
                case true when true:
                    cloud.GameObject.transform.position = new Vector3(
                        leftBound - Random.Range(0.5f, 2f),
                        Random.Range(minY, maxY),
                        0
                    );
                    RandomizeCloudProperties(cloud);
                    break;
                case false when true:
                    cloud.GameObject.transform.position = new Vector3(
                        rightBound + Random.Range(0.5f, 2f),
                        Random.Range(minY, maxY),
                        0
                    );
                    RandomizeCloudProperties(cloud);
                    break;
            }
        }
    }

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
