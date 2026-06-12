using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GasSystem : NetworkBehaviour
{
    public static GasSystem Instance { get; private set; }

    [Header("Gas State")]
    [Tooltip("Local cached gas value. Safe for other scripts to read anytime.")]
    [Range(0f, 1f)] public float gas01 = 0f;

    [Networked, OnChangedRender(nameof(OnGas01NetChanged))]
    private float Gas01Net { get; set; }

    [Header("Leak Causes")]
    public bool hoseLeak = false;
    public List<GasStoveKnobLeakByAngle> stoveKnobs = new();

    [Header("Main Supply")]
    [Tooltip("0 = dong, 1 = mo toi da")]
    [Range(0f, 1f)] public float mainValveOpen01 = 1f;

    [Tooltip("Nho hon nguong nay thi coi nhu van da dong")]
    [SerializeField] private float mainValveOpenThreshold = 0.01f;

    [Header("Vent Sources")]
    public List<GasVentByAngle> vents = new();

    [Header("Level Thresholds (read from gas01)")]
    [Tooltip("gas01 < level1Threshold => Level 0")]
    [Range(0f, 1f)] public float level1Threshold = 0.10f;

    [Tooltip("gas01 < level2Threshold => Level 1")]
    [Range(0f, 1f)] public float level2Threshold = 0.35f;

    [Tooltip("gas01 < level3Threshold => Level 2, con lai la Level 3")]
    [Range(0f, 1f)] public float level3Threshold = 0.70f;

    [Header("Timing")]
    [Tooltip("Van mo max + co leak + khong thong gio: tu gas01 = 0 len 1 mat khoang nay")]
    public float secondsToFullAtMaxLeak = 60f;

    [Tooltip("Moi 1 vent mo max se hut gas theo toc do nay. Nhieu vent cong don that su")]
    public float secondsToClearWithFullVent = 90f;

    [Tooltip("Khong con leak, khong mo cua: gas tu tan rat cham")]
    public float secondsToClearNaturally = 480f;

    [Header("Read Only")]
    [SerializeField] private bool knobLeak = false;
    [SerializeField] private bool leakPresent = false;
    [SerializeField] private bool mainSupplyOpen = false;
    [SerializeField] private int activeOpenings = 0;

    [Tooltip("Tong muc thong gio cong don tu cac vent, co the > 1")]
    public float vent01 = 0f;

    [Tooltip("Do manh leak hien tai, da tinh theo van chinh")]
    [Range(0f, 1f)] public float leakStrength01 = 0f;

    public bool leakActive = false;

    public bool HoseLeak => hoseLeak;
    public bool KnobLeak => knobLeak;
    public bool LeakPresent => leakPresent;
    public bool MainSupplyOpen => mainSupplyOpen;
    public bool LeakActive => leakActive;
    public bool HasGasInRoom => gas01 > 0.001f;

    public event Action<int> GasLevelChanged;
    public int CurrentGasLevel => currentGasLevel;

    public bool IsFusionSpawned => fusionSpawned;
    public bool HasGasAuthority => !fusionSpawned || Object.HasStateAuthority;

    private int currentGasLevel = -1;
    private bool fusionSpawned = false;
    private bool previousLeakActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GasSystem in scene. Destroying duplicate component.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        gas01 = Mathf.Clamp01(gas01);
        currentGasLevel = GasLevel();
        previousLeakActive = leakActive;
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
        {
            // Host uses the inspector/local value as the initial network value.
            Gas01Net = Mathf.Clamp01(gas01);
        }
        else
        {
            // Client reads the network value once Fusion is ready.
            gas01 = Mathf.Clamp01(Gas01Net);
        }

        currentGasLevel = GasLevel();
        previousLeakActive = leakActive;

        RefreshGasLevel(forceEvent: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Single-player / editor fallback:
        // If Fusion has not spawned this object yet, behave like the old GasSystem.
        if (!fusionSpawned)
        {
            SimulateGas(Time.deltaTime, writeToNetwork: false);
            return;
        }

        // In multiplayer, non-host clients do not change gas01.
        // But they may still update derived read-only values for UI/debug.
        if (!Object.HasStateAuthority)
        {
            UpdateKnobLeak();
            UpdateVentilation();
            UpdateDerivedLeakState();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        SimulateGas(Runner.DeltaTime, writeToNetwork: true);
    }

    private void SimulateGas(float deltaTime, bool writeToNetwork)
    {
        UpdateKnobLeak();
        UpdateVentilation();
        UpdateDerivedLeakState();
        CheckLeakEvent();

        float fillRate01PerSec = leakActive
            ? leakStrength01 / Mathf.Max(0.01f, secondsToFullAtMaxLeak)
            : 0f;

        float ventDrainRate01PerSec = vent01 > 0f
            ? vent01 / Mathf.Max(0.01f, secondsToClearWithFullVent)
            : 0f;

        float naturalDrainRate01PerSec = !leakActive
            ? 1f / Mathf.Max(0.01f, secondsToClearNaturally)
            : 0f;

        gas01 += (fillRate01PerSec - ventDrainRate01PerSec - naturalDrainRate01PerSec) * deltaTime;
        gas01 = Mathf.Clamp01(gas01);

        if (writeToNetwork)
            Gas01Net = gas01;

        RefreshGasLevel();
    }

    private void UpdateDerivedLeakState()
    {
        leakPresent = hoseLeak || knobLeak;
        mainSupplyOpen = mainValveOpen01 > mainValveOpenThreshold;

        leakStrength01 = (leakPresent && mainSupplyOpen)
            ? Mathf.Clamp01(mainValveOpen01)
            : 0f;

        leakActive = leakStrength01 > 0.0001f;
    }

    private void OnGas01NetChanged()
    {
        // This is called on clients when Host changes Gas01Net.
        // Keep gas01 as the safe value other scripts can read.
        gas01 = Mathf.Clamp01(Gas01Net);
        RefreshGasLevel();
    }

    private void UpdateKnobLeak()
    {
        knobLeak = false;

        for (int i = 0; i < stoveKnobs.Count; i++)
        {
            var knob = stoveKnobs[i];
            if (!knob) continue;

            if (knob.IsLeaking)
            {
                knobLeak = true;
                break;
            }
        }
    }

    private void UpdateVentilation()
    {
        activeOpenings = 0;
        float ventSum = 0f;

        for (int i = 0; i < vents.Count; i++)
        {
            var v = vents[i];
            if (!v) continue;

            float open01 = Mathf.Clamp01(v.GetOpen01());
            ventSum += open01;

            if (v.IsOpenEnough())
                activeOpenings++;
        }

        // Same as old version: do not clamp to 1.
        // 1 max vent = 1, 2 max vents = 2, etc.
        vent01 = Mathf.Max(0f, ventSum);
    }

    private void CheckLeakEvent()
    {
        if (previousLeakActive == leakActive) return;

        previousLeakActive = leakActive;

        if (!Application.isPlaying) return;

        GameplayEventBus.Raise(
            leakActive ? GameplayEventType.GasLeakStarted : GameplayEventType.GasLeakStopped,
            actorId: "GasSystem",
            targetId: "GasLeak",
            payload: gas01
        );
    }

    public void SetMainValveOpen01(float value)
    {
        value = Mathf.Clamp01(value);

        if (!fusionSpawned)
        {
            mainValveOpen01 = value;
            return;
        }

        if (Object.HasStateAuthority)
        {
            mainValveOpen01 = value;
        }
        else
        {
            RPC_RequestSetMainValveOpen01(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetMainValveOpen01(float value)
    {
        mainValveOpen01 = Mathf.Clamp01(value);
    }

    public void SetHoseLeak(bool value)
    {
        if (!fusionSpawned)
        {
            hoseLeak = value;
            return;
        }

        if (Object.HasStateAuthority)
        {
            hoseLeak = value;
        }
        else
        {
            RPC_RequestSetHoseLeak(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetHoseLeak(bool value)
    {
        hoseLeak = value;
    }

    public void SetGas01(float value)
    {
        value = Mathf.Clamp01(value);

        if (!fusionSpawned)
        {
            gas01 = value;
            RefreshGasLevel();
            return;
        }

        if (Object.HasStateAuthority)
        {
            gas01 = value;
            Gas01Net = value;
            RefreshGasLevel();
        }
        else
        {
            RPC_RequestSetGas01(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetGas01(float value)
    {
        gas01 = Mathf.Clamp01(value);
        Gas01Net = gas01;
        RefreshGasLevel();
    }

    // Helper neu script van cua ban dang dung goc quay -45 -> 135
    public void SetMainValveFromAngle(float currentAngle, float closedAngle = -45f, float openAngle = 135f)
    {
        float open01 = Mathf.InverseLerp(closedAngle, openAngle, currentAngle);
        SetMainValveOpen01(open01);
    }

    public int GasLevel()
    {
        if (gas01 < level1Threshold) return 0;
        if (gas01 < level2Threshold) return 1;
        if (gas01 < level3Threshold) return 2;
        return 3;
    }

    private void RefreshGasLevel(bool forceEvent = false)
    {
        int newLevel = GasLevel();

        if (!forceEvent && newLevel == currentGasLevel) return;

        currentGasLevel = newLevel;

        if (Application.isPlaying)
            GasLevelChanged?.Invoke(currentGasLevel);
    }

    public string GasLevelText()
    {
        switch (GasLevel())
        {
            case 0: return "An toan";
            case 1: return "Mui gas nhe";
            case 2: return "Mui manh";
            case 3: return "Nong nac - nguy hiem";
            default: return "Khong xac dinh";
        }
    }

    public bool IsAtOrAboveLevel(int level)
    {
        return GasLevel() >= level;
    }

    public bool CanIgniteBySpark(int requiredLevel = 2)
    {
        requiredLevel = Mathf.Clamp(requiredLevel, 0, 3);
        return GasLevel() >= requiredLevel && HasGasInRoom;
    }

    public bool CanIgniteByHeat()
    {
        // Lua/nhiet co the bat som hon spark
        return GasLevel() >= 1 && HasGasInRoom;
    }

    public bool CanSustainNozzleFire()
    {
        // Dau voi binh gas tiep tuc chay khi van chinh chua dong
        return leakPresent && mainSupplyOpen;
    }

    public bool AnyOpeningOpen()
    {
        return activeOpenings > 0;
    }

    public int ActiveOpenings()
    {
        return activeOpenings;
    }

    private void OnValidate()
    {
        gas01 = Mathf.Clamp01(gas01);
        mainValveOpen01 = Mathf.Clamp01(mainValveOpen01);

        if (mainValveOpenThreshold < 0f)
            mainValveOpenThreshold = 0f;

        if (secondsToFullAtMaxLeak < 0.01f)
            secondsToFullAtMaxLeak = 0.01f;

        if (secondsToClearWithFullVent < 0.01f)
            secondsToClearWithFullVent = 0.01f;

        if (secondsToClearNaturally < 0.01f)
            secondsToClearNaturally = 0.01f;

        level1Threshold = Mathf.Clamp(level1Threshold, 0f, 0.98f);
        level2Threshold = Mathf.Clamp(level2Threshold, level1Threshold + 0.01f, 0.99f);
        level3Threshold = Mathf.Clamp(level3Threshold, level2Threshold + 0.01f, 1f);
    }
}