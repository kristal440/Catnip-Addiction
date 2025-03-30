using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BackgroundScrollController : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform transform;
        public float scrollSpeed;
        public bool isTilemap = true;
        public float width;
        public bool autoAdjustWidth = true;
    }

    [Header("Parallax Settings")]
    public ParallaxLayer[] layers;
    public Transform cameraTransform;

    [Header("Simple Background Scrolling")]
    public bool useSimpleScrolling;
    public float simpleScrollSpeed = 2f;
    public Renderer simpleBackgroundRenderer;

    private Camera _camera;
    private Vector2 _scrollOffset = Vector2.zero;

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
