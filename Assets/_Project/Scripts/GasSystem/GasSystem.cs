using System.Collections.Generic;
using UnityEngine;

public class GasSystem : MonoBehaviour
{
    [Header("Gas State")]
    [Range(0f, 1f)] public float gas01 = 0f;
    public bool leakActive = true;

    [Header("Demo Timing")]
    [Tooltip("10s = tang 1 level khi tat ca cua dong")]
    public float fillSecondsPerLevel = 10f;

    [Tooltip("10s = giam 1 level cho moi cua/cua so dang mo")]
    public float drainSecondsPerLevelPerOpening = 10f;

    [Header("Openings / Vent Sources")]
    public List<GasVentByAngle> vents = new();

    [Header("Read Only")]
    [SerializeField] private int activeOpenings = 0;
    [Range(0f, 1f)] public float vent01 = 0f;

    private void Update(){
        activeOpenings = 0;
        float ventSum = 0f;

        for (int i = 0; i < vents.Count; i++)
        {
            var v = vents[i];
            if (!v) continue;

            float open01 = v.GetOpen01();
            ventSum += open01;

            if (v.IsOpenEnough())
                activeOpenings++;
        }

        vent01 = vents.Count > 0 ? Mathf.Clamp01(ventSum / vents.Count) : 0f;

        float fillRate01PerSec = leakActive ? 1f / (fillSecondsPerLevel * 3f) : 0f;
        float drainRate01PerSec = activeOpenings > 0
            ? activeOpenings / (drainSecondsPerLevelPerOpening * 3f)
            : 0f;

        // Demo logic:
        // he mo cua la uu tien giam gas ngay
        if (activeOpenings > 0)
            gas01 -= drainRate01PerSec * Time.deltaTime;
        else
            gas01 += fillRate01PerSec * Time.deltaTime;

        gas01 = Mathf.Clamp01(gas01);
    }

    public int GasLevel(){
        // 0 = chua co / gan nhu chua co gas
        if (gas01 <= 0.001f) return 0;

        // 0 -> 30s
        if (gas01 < 0.25f) return 1;

        // 30s -> 120s
        if (gas01 < 1f) return 2;

        // >= 120s
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
}