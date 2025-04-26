using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <inheritdoc />
/// <summary>
/// Manages parallax scrolling background layers with different scroll speeds.
/// </summary>
public class BackgroundScrollController : MonoBehaviour
{
    [Serializable]
    public class ParallaxLayer
    {
        public Transform transform;
        public float scrollSpeed;
        public bool isTilemap = true;
        public float width;
        public bool autoAdjustWidth = true;
    }

    [Header("Parallax Settings")]
    [SerializeField] [Tooltip("Background layers with individual parallax settings")] private ParallaxLayer[] layers;
    [SerializeField] [Tooltip("Reference to the camera transform for position tracking")] public Transform cameraTransform;

    [Header("Simple Background Scrolling")]
    [SerializeField] [Tooltip("Whether to use simple texture offset scrolling instead of parallax")] private bool useSimpleScrolling;
    [SerializeField] [Tooltip("Speed for simple scrolling mode")] private float simpleScrollSpeed = 2f;
    [SerializeField] [Tooltip("Renderer to use for simple scrolling mode")] private Renderer simpleBackgroundRenderer;

    private Camera _camera;
    private Vector2 _scrollOffset = Vector2.zero;

    /// Initialize camera reference and layer widths
    private void Start()
    {
        _camera = Camera.main;
        if (cameraTransform == null)
            if (Camera.main != null)
                cameraTransform = Camera.main.transform;

        foreach (var layer in layers.Where(static layer => layer.transform != null))
            if (layer.isTilemap && layer.autoAdjustWidth && layer.transform.TryGetComponent<Tilemap>(out var tilemap))
                layer.width = tilemap.localBounds.size.x;
    }

    /// Handle scrolling for all layers or simple background
    private void Update()
    {
        if (useSimpleScrolling && simpleBackgroundRenderer)
        {
            _scrollOffset.x += simpleScrollSpeed * Time.deltaTime;
            simpleBackgroundRenderer.material.mainTextureOffset = _scrollOffset;
            return;
        }

        foreach (var layer in layers)
        {
            if (!layer.transform) continue;

            var position = layer.transform.position;
            position.x -= layer.scrollSpeed * Time.deltaTime;

            switch (layer.isTilemap)
            {
                case true when layer.width > 0:
                {
                    var viewportLeftEdge = _camera.ViewportToWorldPoint(Vector3.zero).x;
                    var tileRightEdge = position.x + layer.width;

                    if (tileRightEdge < viewportLeftEdge)
                        position.x += layer.width;

                    break;
                }
                case false when layer.transform.TryGetComponent<Renderer>(out var component):
                {
                    var material = component.material;
                    var offset = material.mainTextureOffset;
                    offset.x += layer.scrollSpeed * Time.deltaTime;
                    material.mainTextureOffset = offset;
                    continue;
                }
            }

            layer.transform.position = position;
        }
    }
}
