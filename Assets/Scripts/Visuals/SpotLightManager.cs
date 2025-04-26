using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <inheritdoc />
/// <summary>
/// Manages spotlight effects with configurable properties and network synchronization
/// </summary>
public class SpotLightManager : MonoBehaviourPun
{
    [Header("Light Settings")]
    [SerializeField] [Tooltip("Reference to the 2D spotlight component")]
    private Light2D spotLight;
    [SerializeField] [Range(0f, 2f)] [Tooltip("Controls how quickly light intensity diminishes with distance")]
    private float falloffStrength = 1f;
    [SerializeField] [Tooltip("Direction the spotlight will point")]
    private Vector2 spotDirection = new(0, -1);

    [Header("Visual Effects")]
    [SerializeField] [Tooltip("Enable flickering effect for more dynamic lighting")]
    private bool useFlicker;
    [SerializeField] [Range(0f, 1f)] [Tooltip("How strong the flickering effect will be")]
    private float flickerIntensity = 0.1f;
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("How fast the light will flicker")]
    private float flickerSpeed = 1f;

    [Header("Mobile Optimization")]
    [SerializeField] [Tooltip("Reduces light quality on mobile for better performance")]
    private bool optimizeForMobile = true;
    [SerializeField] [Tooltip("Resolution of the light in pixels when optimized")]
    private int mobilePixelResolution = 64;

    private float _baseIntensity;
    private Coroutine _flickerCoroutine;

    // Creates a spotlight if needed and initializes light settings
    private void Awake()
    {
        if (spotLight == null)
        {
            var lightObj = new GameObject("Spot Light")
            {
                transform =
                {
                    parent = transform,
                    localPosition = Vector3.zero
                }
            };
            spotLight = lightObj.AddComponent<Light2D>();
            spotLight.lightType = Light2D.LightType.Point;
        }

        _baseIntensity = spotLight.intensity;
        ApplyLightSettings();
    }

    // Stops flickering when disabled
    private void OnDisable()
    {
        if (_flickerCoroutine == null) return;

        StopCoroutine(_flickerCoroutine);
        _flickerCoroutine = null;
    }

    // Starts flickering if enabled
    private void Start()
    {
        if (!useFlicker || !Application.isPlaying) return;

        if (_flickerCoroutine != null)
            StopCoroutine(_flickerCoroutine);
        _flickerCoroutine = StartCoroutine(FlickerLight());
    }

    // Applies configured settings to the spotlight
    private void ApplyLightSettings()
    {
        if (spotLight == null) return;

        if (_baseIntensity <= 0)
            _baseIntensity = spotLight.intensity > 0 ? spotLight.intensity : 1f;

        spotLight.lightType = Light2D.LightType.Point;

        if (!useFlicker || !Application.isPlaying)
            spotLight.intensity = _baseIntensity;

        var angle = Mathf.Atan2(spotDirection.y, spotDirection.x) * Mathf.Rad2Deg - 90f;
        spotLight.transform.localRotation = Quaternion.Euler(0, 0, angle);


        spotLight.shadowsEnabled = true;
        spotLight.shadowIntensity = 0.7f;

        spotLight.falloffIntensity = falloffStrength;

        if (!optimizeForMobile) return;

        spotLight.shapeLightFalloffSize = 1f;

        switch (mobilePixelResolution)
        {
            case <= 32:
                spotLight.shadowIntensity = 0.4f;
                spotLight.shapeLightFalloffSize = 0.5f;
                break;
            case <= 64:
                spotLight.shadowIntensity = 0.6f;
                spotLight.shapeLightFalloffSize = 0.75f;
                break;
            default:
                spotLight.shadowIntensity = 0.8f;
                spotLight.shapeLightFalloffSize = 1.0f;
                break;
        }
    }

    // Creates a flickering effect using Perlin noise
    private IEnumerator FlickerLight()
    {
        var waitTime = new WaitForSeconds(0.05f);

        while (useFlicker && Application.isPlaying)
        {
            var noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0) * 2 - 1;
            var flickerAmount = noise * flickerIntensity;

            if (spotLight)
                spotLight.intensity = _baseIntensity + flickerAmount;

            yield return waitTime;
        }
    }

    // Synchronizes light settings across network
    [PunRPC]
    public void SyncLightSettings(float syncedIntensity, float syncedRadius, float r, float g, float b, float syncedFalloff, float syncedAngle)
    {
        falloffStrength = syncedFalloff;
        ApplyLightSettings();
    }

    // Enables or disables light flickering
    private void ToggleFlicker(bool enableFlicker)
    {
        useFlicker = enableFlicker;

        if (_flickerCoroutine != null)
        {
            StopCoroutine(_flickerCoroutine);
            _flickerCoroutine = null;
        }

        switch (useFlicker)
        {
            case false when spotLight != null:
                spotLight.intensity = _baseIntensity;
                break;
            case true when Application.isPlaying:
                _flickerCoroutine = StartCoroutine(FlickerLight());
                break;
        }

        if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
            photonView.RPC(nameof(SyncFlickerState), RpcTarget.Others, useFlicker);
    }

    // Synchronizes flicker state across network
    [PunRPC]
    public void SyncFlickerState(bool enableFlicker)
    {
        ToggleFlicker(enableFlicker);
    }

    // Updates light settings and sends to network
    private void UpdateLightSettings()
    {
        if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
            photonView.RPC("SyncLightSettings", RpcTarget.Others);
    }

    // Sets light properties and synchronizes changes
    public void SetLightProperties(float newFalloffStrength = -1, float newSpotAngle = -1)
    {
        if (newFalloffStrength >= 0)
            falloffStrength = newFalloffStrength;

        if (newSpotAngle >= 0) { }

        ApplyLightSettings();
        UpdateLightSettings();
    }

    #if UNITY_EDITOR
    // Updates light settings when properties change in editor
    private void OnValidate()
    {
        ApplyLightSettings();
    }
    #endif
}
