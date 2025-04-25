using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Photon.Pun;

public class SpotLightManager : MonoBehaviourPun
{
    [Header("Light Settings")]
    [SerializeField] private Light2D spotLight;
    [SerializeField] [Range(0f, 2f)] private float falloffStrength = 1f;
    [SerializeField] private Vector2 spotDirection = new(0, -1);

    [Header("Visual Effects")]
    [SerializeField] private bool useFlicker;
    [SerializeField] [Range(0f, 1f)] private float flickerIntensity = 0.1f;
    [SerializeField] [Range(0.1f, 5f)] private float flickerSpeed = 1f;

    [Header("Mobile Optimization")]
    [SerializeField] private bool optimizeForMobile = true;
    [SerializeField] private int mobilePixelResolution = 64;

    private float _baseIntensity;
    private Coroutine _flickerCoroutine;

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

    private void OnDisable()
    {
        if (_flickerCoroutine == null) return;

        StopCoroutine(_flickerCoroutine);
        _flickerCoroutine = null;
    }

    private void Start()
    {
        if (!useFlicker || !Application.isPlaying) return;

        if (_flickerCoroutine != null)
            StopCoroutine(_flickerCoroutine);
        _flickerCoroutine = StartCoroutine(FlickerLight());
    }

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

    [PunRPC]
    public void SyncLightSettings(float syncedIntensity, float syncedRadius, float r, float g, float b, float syncedFalloff, float syncedAngle)
    {
        falloffStrength = syncedFalloff;
        ApplyLightSettings();
    }

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

    [PunRPC]
    public void SyncFlickerState(bool enableFlicker)
    {
        ToggleFlicker(enableFlicker);
    }

    private void UpdateLightSettings()
    {
        if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
            photonView.RPC("SyncLightSettings", RpcTarget.Others);
    }

    public void SetLightProperties(float newFalloffStrength = -1, float newSpotAngle = -1)
    {
        if (newFalloffStrength >= 0)
            falloffStrength = newFalloffStrength;

        if (newSpotAngle >= 0) { }

        ApplyLightSettings();
        UpdateLightSettings();
    }

    public float FalloffStrength
    {
        get => falloffStrength;
        set
        {
            falloffStrength = value;
            ApplyLightSettings();
            UpdateLightSettings();
        }
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLightSettings();
    }
    #endif
}
