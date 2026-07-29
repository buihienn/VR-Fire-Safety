using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LightSwitch : NetworkBehaviour
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [Tooltip("Rotation Y khi đèn đang tắt.")]
    [SerializeField] private float offAngle = 98f;
    [Tooltip("Rotation Y khi đèn đang bật.")]
    [SerializeField] private float onAngle = 82f;

    [Header("Light Targets")]
    [SerializeField] private Light[] lightComponents;
    [SerializeField] private GameObject[] lightObjects;
    [SerializeField] private Transform lightGroupRoot;
    [SerializeField] private bool autoFindLightsWhenTargetsEmpty = true;
    [SerializeField] private string fallbackLightGroupName = "CeilingLightGroup";
    [SerializeField] private bool controlEmissionUnderLightGroup = true;

    [Header("State")]
    [SerializeField] private bool startOn;

    [Header("Gas Safety")]
    [Tooltip("Đèn chỉ được phép bật khi gas level không vượt quá giá trị này.")]
    [Range(0, 3)]
    [SerializeField] private int maximumOperatingGasLevel = 1;

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool currentState;

    [Networked, OnChangedRender(nameof(OnLightNetworkChanged))]
    private NetworkBool IsOnNet { get; set; }

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private readonly List<EmissionTarget> emissionTargets =
        new List<EmissionTarget>();

    public bool IsOn => fusionSpawned ? (bool)IsOnNet : currentState;

    private void Awake()
    {
        ResolveLightTargets();
        CacheEmissionTargets();
        ApplyStateInstant(startOn);
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
            IsOnNet = startOn;

        ApplyStateInstant((bool)IsOnNet);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!(bool)IsOnNet) return;
        if (CanOperateAtCurrentGasLevel()) return;

        SetLightOnStateAuthority(false);
    }

    private void Update()
    {
        if (fusionSpawned) return;
        if (!currentState) return;
        if (CanOperateAtCurrentGasLevel()) return;

        ApplyStateInstant(false);
    }

    public void PressButton()
    {
        RequestLightState(!IsOn);
    }

    public void SetLightState(bool value)
    {
        RequestLightState(value);
    }

    [ContextMenu("Debug/Turn Light On")]
    public void DebugTurnLightOn()
    {
        DebugSetLightState(true);
    }

    [ContextMenu("Debug/Turn Light Off")]
    public void DebugTurnLightOff()
    {
        DebugSetLightState(false);
    }

    [ContextMenu("Debug/Toggle Light")]
    public void DebugToggleLight()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "LightSwitch: Hãy vào Play Mode trước khi dùng nút Debug.",
                this);
            return;
        }

        DebugSetLightState(!currentState);
    }

    private void DebugSetLightState(bool value)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "LightSwitch: Hãy vào Play Mode trước khi dùng nút Debug.",
                this);
            return;
        }

        if (fusionSpawned && Object != null && Object.HasStateAuthority)
        {
            IsOnNet = value;
            ApplyStateInstant(value);
            RPC_ApplyLightState(value);
            return;
        }

        ApplyStateInstant(value);
    }

    private void RequestLightState(bool requestedState)
    {
        if (!fusionSpawned)
        {
            ApplyStateInstant(CanAcceptState(requestedState));
            return;
        }

        if (Object.HasStateAuthority)
            SetLightOnStateAuthority(requestedState);
        else
            RPC_RequestSetLight(requestedState);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void RPC_RequestSetLight(bool requestedState)
    {
        SetLightOnStateAuthority(requestedState);
    }

    private void SetLightOnStateAuthority(bool requestedState)
    {
        if (!Object.HasStateAuthority)
            return;

        bool acceptedState = CanAcceptState(requestedState);
        IsOnNet = acceptedState;
        ApplyStateInstant(acceptedState);
        RPC_ApplyLightState(acceptedState);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_ApplyLightState(bool acceptedState)
    {
        ApplyStateInstant(acceptedState);
    }

    private void OnLightNetworkChanged()
    {
        ApplyStateInstant((bool)IsOnNet);
    }

    private bool CanAcceptState(bool requestedState)
    {
        return !requestedState || CanOperateAtCurrentGasLevel();
    }

    private bool CanOperateAtCurrentGasLevel()
    {
        return GasSystem.Instance == null ||
               GasSystem.Instance.GasLevel() <= maximumOperatingGasLevel;
    }

    private void ResolveLightTargets()
    {
        if (lightGroupRoot == null && !string.IsNullOrWhiteSpace(fallbackLightGroupName))
        {
            GameObject lightGroup = GameObject.Find(fallbackLightGroupName);
            if (lightGroup != null)
                lightGroupRoot = lightGroup.transform;
        }

        bool targetsAreEmpty = lightComponents == null || lightComponents.Length == 0;
        if (autoFindLightsWhenTargetsEmpty && targetsAreEmpty && lightGroupRoot != null)
            lightComponents = lightGroupRoot.GetComponentsInChildren<Light>(true);

        if (lightComponents == null)
            lightComponents = new Light[0];

        if (lightObjects == null)
            lightObjects = new GameObject[0];

        if (lightComponents.Length == 0)
        {
            Debug.LogWarning(
                $"LightSwitch: Không tìm thấy Light nào trong nhóm '{fallbackLightGroupName}'.",
                this);
        }
    }

    private void CacheEmissionTargets()
    {
        emissionTargets.Clear();

        if (!controlEmissionUnderLightGroup || lightGroupRoot == null)
            return;

        Renderer[] renderers = lightGroupRoot.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            Material[] materials = targetRenderer.sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.HasProperty(EmissionColorId))
                    continue;

                emissionTargets.Add(new EmissionTarget
                {
                    Renderer = targetRenderer,
                    MaterialIndex = materialIndex,
                    OnColor = material.GetColor(EmissionColorId)
                });
            }
        }
    }

    private void ApplyStateInstant(bool value)
    {
        currentState = value;
        SetButtonVisual(value ? onAngle : offAngle);

        for (int i = 0; i < lightComponents.Length; i++)
        {
            if (lightComponents[i] != null)
                lightComponents[i].enabled = value;
        }

        for (int i = 0; i < lightObjects.Length; i++)
        {
            if (lightObjects[i] != null)
                lightObjects[i].SetActive(value);
        }

        ApplyEmissionState(value);
    }

    private void SetButtonVisual(float yAngle)
    {
        if (buttonVisual == null)
            return;

        Vector3 euler = buttonVisual.localEulerAngles;
        euler.y = yAngle;
        buttonVisual.localEulerAngles = euler;
    }

    private void ApplyEmissionState(bool value)
    {
        var properties = new MaterialPropertyBlock();

        for (int i = 0; i < emissionTargets.Count; i++)
        {
            EmissionTarget target = emissionTargets[i];
            if (target.Renderer == null)
                continue;

            target.Renderer.GetPropertyBlock(properties, target.MaterialIndex);
            properties.SetColor(
                EmissionColorId,
                value ? target.OnColor : Color.black);
            target.Renderer.SetPropertyBlock(properties, target.MaterialIndex);
            properties.Clear();
        }
    }

    private struct EmissionTarget
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color OnColor;
    }
}
