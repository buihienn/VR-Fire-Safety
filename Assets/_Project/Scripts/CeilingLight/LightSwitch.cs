using System.Collections.Generic;
using UnityEngine;

public class LightSwitch : NetworkBehaviour
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private float offAngle = 98f;
    [SerializeField] private float onAngle = 82f;

    [Header("Targets")]
    [SerializeField] private Light[] lightComponents;
    [SerializeField] private GameObject[] lightObjects;
    [SerializeField] private Transform lightGroupRoot;
    [SerializeField] private string fallbackLightGroupName = "CeilingLightGroup";

    [Header("Automatic Scene Targets")]
    [SerializeField] private bool autoFindLightsWhenTargetsEmpty = true;
    [SerializeField] private Transform automaticLightRoot;
    [SerializeField] private string automaticLightRootName = "CeilingLightGroup";
    [SerializeField] private bool controlEmissionUnderAutomaticRoot = true;

    [Header("State")]
    [SerializeField] private bool startOn;

<<<<<<< Updated upstream
    [Header("Gas Rule")]
    [Tooltip("Đèn chỉ được phép bật khi gas level không vượt quá giá trị này.")]
    [Range(0, 3)]
    [SerializeField] private int maximumOperatingGasLevel = 1;
=======
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<EmissionTarget> emissionTargets = new List<EmissionTarget>();
    private bool isOn;
>>>>>>> Stashed changes

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool isOn;

    [Networked, OnChangedRender(nameof(OnLightNetworkChanged))]
    private NetworkBool IsOnNet { get; set; }

    public bool IsOn => fusionSpawned ? (bool)IsOnNet : isOn;

    private void Awake()
    {
<<<<<<< Updated upstream
        ResolveLightTargets();
        isOn = startOn && CanOperateAtCurrentGasLevel();
        ApplyStateInstant(isOn);
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
            IsOnNet = isOn && CanOperateAtCurrentGasLevel();

        ApplyStateInstant(IsOnNet);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsOnNet) return;
        if (CanOperateAtCurrentGasLevel()) return;

        SetLightOnStateAuthority(false);
    }

    private void Update()
    {
        if (fusionSpawned) return;
        if (!isOn) return;
        if (CanOperateAtCurrentGasLevel()) return;

        ApplyStateInstant(false);
=======
        ResolveAutomaticTargets();
        isOn = startOn;
        ApplyStateInstant();
>>>>>>> Stashed changes
    }

    public void PressButton()
    {
        bool requestedState = !IsOn;

        if (!fusionSpawned)
        {
            bool acceptedState =
                !requestedState || CanOperateAtCurrentGasLevel();

            ApplyStateInstant(acceptedState && requestedState);
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

        bool acceptedState =
            !requestedState || CanOperateAtCurrentGasLevel();

        IsOnNet = acceptedState && requestedState;
        ApplyStateInstant(IsOnNet);
        RPC_ApplyLightState(IsOnNet);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_ApplyLightState(bool acceptedState)
    {
        ApplyStateInstant(acceptedState);
    }

    private void OnLightNetworkChanged()
    {
        ApplyStateInstant(IsOnNet);
    }

    private bool CanOperateAtCurrentGasLevel()
    {
        return GasSystem.Instance != null &&
               GasSystem.Instance.GasLevel() <= maximumOperatingGasLevel;
    }

    private void ResolveLightTargets()
    {
        if (lightComponents != null && lightComponents.Length > 0)
            return;

        if (lightGroupRoot == null && !string.IsNullOrWhiteSpace(fallbackLightGroupName))
        {
            GameObject lightGroup = GameObject.Find(fallbackLightGroupName);
            if (lightGroup != null)
                lightGroupRoot = lightGroup.transform;
        }

        if (lightGroupRoot != null)
            lightComponents = lightGroupRoot.GetComponentsInChildren<Light>(true);
    }

    private void ApplyStateInstant(bool value)
    {
        isOn = value;
        SetButtonVisual(isOn ? onAngle : offAngle);

        if (lightComponents != null)
        {
            for (int i = 0; i < lightComponents.Length; i++)
            {
                if (lightComponents[i] != null)
                    lightComponents[i].enabled = isOn;
            }
        }

        if (lightObjects != null)
        {
            for (int i = 0; i < lightObjects.Length; i++)
            {
                if (lightObjects[i] != null)
                    lightObjects[i].SetActive(isOn);
            }
        }

        ApplyEmissionState();
    }

    private void SetButtonVisual(float yAngle)
    {
        if (buttonVisual == null) return;

        Vector3 euler = buttonVisual.localEulerAngles;
        euler.y = yAngle;
        buttonVisual.localEulerAngles = euler;
    }
<<<<<<< Updated upstream
=======

    private void ResolveAutomaticTargets()
    {
        bool targetsAreEmpty = lightComponents == null || lightComponents.Length == 0;
        if (!autoFindLightsWhenTargetsEmpty || !targetsAreEmpty)
            return;

        if (automaticLightRoot == null && !string.IsNullOrWhiteSpace(automaticLightRootName))
        {
            GameObject rootObject = GameObject.Find(automaticLightRootName);
            if (rootObject != null)
                automaticLightRoot = rootObject.transform;
        }

        if (automaticLightRoot == null)
        {
            Debug.LogWarning(
                $"LightSwitch: Không tìm thấy nhóm đèn '{automaticLightRootName}'.",
                this);
            lightComponents = new Light[0];
            return;
        }

        lightComponents = automaticLightRoot.GetComponentsInChildren<Light>(true);

        if (controlEmissionUnderAutomaticRoot)
            CacheEmissionTargets(automaticLightRoot);
    }

    private void CacheEmissionTargets(Transform root)
    {
        emissionTargets.Clear();

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

    private void ApplyEmissionState()
    {
        for (int i = 0; i < emissionTargets.Count; i++)
        {
            EmissionTarget target = emissionTargets[i];
            var properties = new MaterialPropertyBlock();
            target.Renderer.GetPropertyBlock(properties, target.MaterialIndex);
            properties.SetColor(
                EmissionColorId,
                isOn ? target.OnColor : Color.black);
            target.Renderer.SetPropertyBlock(properties, target.MaterialIndex);
        }
    }

    private struct EmissionTarget
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color OnColor;
    }
>>>>>>> Stashed changes
}
