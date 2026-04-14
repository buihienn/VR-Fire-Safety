using System.Collections.Generic;
using UnityEngine;

public class GasSystem : MonoBehaviour
{
    [Header("Gas State")]
    [Range(0f, 1f)] public float gas01 = 0f;

    [Header("Leak Causes")]
    [Tooltip("Ro ri o hose. Hien tai co the set tay trong Inspector hoac tu script khac.")]
    public bool hoseLeak = false;

    [Tooltip("Lay leak tu cac knob cua bep")]
    public List<GasStoveKnobLeakByAngle> stoveKnobs = new();

    [Header("Main Supply")]
    [Tooltip("Do mo cua van chinh binh gas. 0 = dong, 1 = mo toi da")]
    [Range(0f, 1f)] public float mainValveOpen01 = 1f;

    [Tooltip("Nho hon nguong nay thi coi nhu van da dong, khong con cap gas")]
    [SerializeField] private float mainValveOpenThreshold = 0.01f;

    [Header("Read Only")]
    [SerializeField] private bool knobLeak = false;
    [SerializeField] private bool leakPresent = false;
    [SerializeField] private bool mainSupplyOpen = false;
    public bool leakActive = false;
    [Range(0f, 1f)] public float leakStrength01 = 0f;

    [Header("Demo Timing")]
    [Tooltip("10s = tang 1 level khi van mo toi da va co leak")]
    public float fillSecondsPerLevel = 10f;

    [Tooltip("10s = giam 1 level cho moi cua/cua so dang mo")]
    public float drainSecondsPerLevelPerOpening = 10f;

    [Header("Openings / Vent Sources")]
    public List<GasVentByAngle> vents = new();

    [Header("Read Only")]
    [SerializeField] private int activeOpenings = 0;
    [Range(0f, 1f)] public float vent01 = 0f;

    public bool HoseLeak => hoseLeak;
    public bool KnobLeak => knobLeak;
    public bool LeakPresent => leakPresent;
    public bool MainSupplyOpen => mainSupplyOpen;

    private void Update()
    {
        UpdateKnobLeak();
        UpdateVentilation();

        // Leak source o downstream:
        // - hoseLeak
        // - hoac co knob dang mo/ro
        leakPresent = hoseLeak || knobLeak;

        // Nguon cap gas chi con khi van chinh con mo
        mainSupplyOpen = mainValveOpen01 > mainValveOpenThreshold;

        // Chi active khi VUA co diem ro VUA con nguon cap gas
        leakStrength01 = (leakPresent && mainSupplyOpen)
            ? Mathf.Clamp01(mainValveOpen01)
            : 0f;

        leakActive = leakStrength01 > 0.001f;

        float fillRate01PerSec = leakStrength01 / Mathf.Max(0.01f, fillSecondsPerLevel * 3f);
        float drainRate01PerSec = activeOpenings > 0
            ? activeOpenings / Mathf.Max(0.01f, drainSecondsPerLevelPerOpening * 3f)
            : 0f;

        gas01 += (fillRate01PerSec - drainRate01PerSec) * Time.deltaTime;
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

        vent01 = vents.Count > 0 ? Mathf.Clamp01(ventSum / vents.Count) : 0f;
    }

    public void SetMainValveOpen01(float value)
    {
        mainValveOpen01 = Mathf.Clamp01(value);
    }

    public void SetHoseLeak(bool value)
    {
        hoseLeak = value;
    }

    public int GasLevel()
    {
        if (gas01 <= 0.01f) return 0;
        if (gas01 < 0.33f) return 1;
        if (gas01 < 0.66f) return 2;
        return 3;
    }

    public bool AnyOpeningOpen() => activeOpenings > 0;
    public int ActiveOpenings() => activeOpenings;

    public string GasLevelText()
    {
        return GasLevel() switch
        {
            0 => "An toan",
            1 => "Mui gas nhe",
            2 => "Mui manh",
            3 => "Nong nac - nguy hiem",
            _ => "Khong xac dinh"
        };
    }

    private void OnValidate()
    {
        if (mainValveOpenThreshold < 0f) mainValveOpenThreshold = 0f;
        if (fillSecondsPerLevel < 0.01f) fillSecondsPerLevel = 0.01f;
        if (drainSecondsPerLevelPerOpening < 0.01f) drainSecondsPerLevelPerOpening = 0.01f;
    }
}