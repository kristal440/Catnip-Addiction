using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(SpriteRenderer))]
public class PixelArtShadowCaster : MonoBehaviour
{
    [Header("Shadow Settings")]
    [SerializeField] private bool castsShadows = true;
    [SerializeField] private bool receivesShadows = true;

    [Header("Shadow Shape")]
    [SerializeField] private ShadowShapeType shadowShape = ShadowShapeType.FromSprite;
    [SerializeField] private Vector2 customShadowOffset = Vector2.zero;
    [SerializeField] private Vector2 customShadowSize = Vector2.one;

    private enum ShadowShapeType
    {
        FromSprite,
        Circle,
        Rectangle
    }

    private ShadowCaster2D _shadowCaster;
    private CompositeCollider2D _compositeCollider;

    private void Awake()
    {
        GetComponent<SpriteRenderer>();

        // Add ShadowCaster2D if it doesn't exist
        _shadowCaster = GetComponent<ShadowCaster2D>();
        if (_shadowCaster == null)
            _shadowCaster = gameObject.AddComponent<ShadowCaster2D>();

        SetupShadowCaster();
    }

    private void SetupShadowCaster()
    {
        if (_shadowCaster == null) return;

        // Configure shadow caster
        _shadowCaster.enabled = castsShadows;
        _shadowCaster.selfShadows = receivesShadows;

        // Use sprite shape for shadow by default
        if (shadowShape == ShadowShapeType.FromSprite)
        {
            // Use the sprite's shape for casting shadows
            // Replaced deprecated useRendererSilhouette property
            _shadowCaster.castsShadows = castsShadows;
            // The selfShadows property is already set above
        }
        else
        {
            // Use a custom shape
            _shadowCaster.castsShadows = castsShadows;
            CreateCustomShadowShape();
        }
    }

    private void CreateCustomShadowShape()
    {
        // Clean up any existing collider
        var existingCollider = GetComponent<CompositeCollider2D>();
        if (existingCollider != null)
            DestroyImmediate(existingCollider);

        var rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }

        // Create a shape based on the selected type
        switch (shadowShape)
        {
            case ShadowShapeType.Circle:
                CreateCircleShadow();
                break;
            case ShadowShapeType.Rectangle:
                CreateRectangleShadow();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void CreateCircleShadow()
    {
        var circleCollider = gameObject.AddComponent<CircleCollider2D>();
        circleCollider.offset = customShadowOffset;
        circleCollider.radius = customShadowSize.x / 2f;
        // Replace deprecated usedByComposite with compositeOperation
        circleCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        _compositeCollider = gameObject.AddComponent<CompositeCollider2D>();
        _compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    private void CreateRectangleShadow()
    {
        var boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.offset = customShadowOffset;
        boxCollider.size = customShadowSize;
        // Replace deprecated usedByComposite with compositeOperation
        boxCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        _compositeCollider = gameObject.AddComponent<CompositeCollider2D>();
        _compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    // Call this when you add this component in multiplayer
    public void SyncShadowSettings()
    {
        // Here you could implement Photon RPC to sync shadow settings
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (_shadowCaster != null)
            SetupShadowCaster();
    }
    #endif
}
