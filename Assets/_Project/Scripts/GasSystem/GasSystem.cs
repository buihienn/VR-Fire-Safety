using UnityEngine;

public class GasSystem : MonoBehaviour
{
    [Header("Gas State (0..1)")]
    [Range(0f, 1f)] public float gas01 = 0.2f;
    public bool leakActive = true;
    [Range(0f, 1f)] public float vent01 = 0f;

    [Header("Timing")]
    public float increaseToMaxSeconds = 300f;        // leak: lên max sau ~5 phút
    public float ventToZeroSecondsAtFullOpen = 180f; // vent=1: về 0 sau ~3 phút

    [Header("Fog Link (optional)")]
    public ParticleSystem gasFog;        // kéo GasFogPS vào đây
    public float maxEmission = 200f;     // gas01=1 => emission = maxEmission
    public float stopThreshold = 0.02f;  // dưới mức này thì Stop()

    [Header("Debug (optional)")]
    public bool logToConsole = true;
    public float logInterval = 0.5f;

    float _logT;

    void Awake()
    {
        if (!gasFog) gasFog = FindFirstObjectByType<ParticleSystem>(); // optional
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // tăng do rò
        if (leakActive)
            gas01 = Mathf.Clamp01(gas01 + dt / Mathf.Max(1f, increaseToMaxSeconds));

        // giảm do cửa mở (mở càng nhiều giảm càng nhanh)
        float ventRate = vent01 * (dt / Mathf.Max(1f, ventToZeroSecondsAtFullOpen));
        gas01 = Mathf.Clamp01(gas01 - ventRate);

        ApplyFog();
        DebugLogTick(dt);
    }

    void ApplyFog()
    {
        if (!gasFog) return;

        var em = gasFog.emission;
        em.enabled = true;

        float rate = gas01 * maxEmission;
        em.rateOverTime = rate; // constant

        // Play/Stop để bạn thấy rõ thay đổi
        if (rate > stopThreshold)
        {
            if (!gasFog.isPlaying) gasFog.Play();
        }
        else
        {
            if (gasFog.isPlaying) gasFog.Stop();
        }
    }

    void DebugLogTick(float dt)
    {
        if (!logToConsole) return;

        _logT += dt;
        if (_logT < logInterval) return;
        _logT = 0f;

        float emission = -1;
        int pCount = -1;
        if (gasFog)
        {
            emission = gasFog.emission.rateOverTime.constant;
            pCount = gasFog.particleCount;
        }

        Debug.Log($"[GAS] gas01={gas01:0.00} L={GasLevel()} vent01={vent01:0.00} leak={leakActive} em={emission:0} pCount={pCount}");
    }

    public int GasLevel()
    {
        if (gas01 < 0.10f) return 0;
        if (gas01 < 0.40f) return 1;
        if (gas01 < 0.75f) return 2;
        return 3;
    }

    public void SetVent01(float v) => vent01 = Mathf.Clamp01(v);
}