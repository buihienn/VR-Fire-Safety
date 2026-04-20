using System.Collections.Generic;
using UnityEngine;

public class GasSystem : MonoBehaviour
{
    public static GasSystem Instance { get; private set; }

    [Header("Gas State")]
    [Range(0f, 1f)] public float gas01 = 0f;

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
    public float secondsToFullAtMaxLeak = 300f;

    [Tooltip("Thong gio mo manh: tu gas01 = 1 ve 0 mat khoang nay")]
    public float secondsToClearWithFullVent = 180f;

    [Tooltip("Khong con leak, khong mo cua: gas tu tan rat cham")]
    public float secondsToClearNaturally = 600f;

    [Header("Read Only")]
    [SerializeField] private bool knobLeak = false;
    [SerializeField] private bool leakPresent = false;
    [SerializeField] private bool mainSupplyOpen = false;
    [SerializeField] private int activeOpenings = 0;

    [Tooltip("Tong muc thong gio, da clamp 0..1")]
    [Range(0f, 1f)] public float vent01 = 0f;

    [Tooltip("Do manh leak hien tai, da tinh theo van chinh")]
    [Range(0f, 1f)] public float leakStrength01 = 0f;

    public bool leakActive = false;

    public bool HoseLeak => hoseLeak;
    public bool KnobLeak => knobLeak;
    public bool LeakPresent => leakPresent;
    public bool MainSupplyOpen => mainSupplyOpen;
    public bool LeakActive => leakActive;
    public bool HasGasInRoom => gas01 > 0.001f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GasSystem in scene. Destroying duplicate component.", this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UpdateKnobLeak();
        UpdateVentilation();

        leakPresent = hoseLeak || knobLeak;
        mainSupplyOpen = mainValveOpen01 > mainValveOpenThreshold;

        // Chi co leak active khi:
        // 1) co diem ro
        // 2) van chinh van con mo
        leakStrength01 = (leakPresent && mainSupplyOpen)
            ? Mathf.Clamp01(mainValveOpen01)
            : 0f;

        leakActive = leakStrength01 > 0.0001f;

        // Fill:
        // leak max => full trong secondsToFullAtMaxLeak
        float fillRate01PerSec = leakActive
            ? leakStrength01 / Mathf.Max(0.01f, secondsToFullAtMaxLeak)
            : 0f;

        // Vent drain:
        // vent01 = 1 => clear trong secondsToClearWithFullVent
        float ventDrainRate01PerSec = vent01 > 0f
            ? vent01 / Mathf.Max(0.01f, secondsToClearWithFullVent)
            : 0f;

        // Natural drain:
        // Chi ap dung khi da khong con leak active
        float naturalDrainRate01PerSec = !leakActive
            ? 1f / Mathf.Max(0.01f, secondsToClearNaturally)
            : 0f;

        gas01 += (fillRate01PerSec - ventDrainRate01PerSec - naturalDrainRate01PerSec) * Time.deltaTime;
        gas01 = Mathf.Clamp01(gas01);
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

        // Tong hieu qua thong gio.
        // 2 cua mo vua vua co the cong don, nhung clamp toi da 1.
        vent01 = Mathf.Clamp01(ventSum);
    }

    public void SetMainValveOpen01(float value)
    {
        mainValveOpen01 = Mathf.Clamp01(value);
    }

    public void SetHoseLeak(bool value)
    {
        hoseLeak = value;
    }

    public void SetGas01(float value)
    {
        gas01 = Mathf.Clamp01(value);
    }

    // Helper neu script van cua ban dang dung goc quay -45 -> 135
    public void SetMainValveFromAngle(float currentAngle, float closedAngle = -45f, float openAngle = 135f)
    {
        mainValveOpen01 = Mathf.InverseLerp(closedAngle, openAngle, currentAngle);
    }

    public int GasLevel()
    {
        if (gas01 < level1Threshold) return 0;
        if (gas01 < level2Threshold) return 1;
        if (gas01 < level3Threshold) return 2;
        return 3;
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