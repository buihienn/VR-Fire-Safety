using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

public class GasSystem : NetworkBehaviour
{
    public static GasSystem Instance { get; private set; }

    [Header("Gas State")]
    [Tooltip("Local cached gas value. Safe for other scripts to read anytime.")]
    [Range(0f, 1f)] public float gas01 = 0f;

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float Gas01Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float MainValveOpen01Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private bool HoseLeakNet { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float LeakStrength01Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private bool LeakActiveNet { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float OpeningAngle0Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float OpeningAngle1Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float OpeningAngle2Net { get; set; }

    [Networked, OnChangedRender(nameof(OnGasStateNetChanged))]
    private float OpeningAngle3Net { get; set; }

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

    [Header("Synchronized Openings")]
    [Tooltip("Goc bat dau duoc tinh la dang mo/thong gio.")]
    [SerializeField] private float synchronizedOpeningActiveAngle = 10f;

    [Tooltip("Goc duoc tinh la mo hoan toan cho muc dich thong gio.")]
    [SerializeField] private float synchronizedOpeningFullAngle = 100f;

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

    [Tooltip("Thoi gian de gas giam tu 1 ve 0 khi co dung 1 opening mo hoan toan va khong con leak.")]
    public float secondsToClearWithFullVent = 40f;

    [FormerlySerializedAs("maximumCombinedVentilation01")]
    [Tooltip("He so thong gio khi ca 2 opening deu mo hoan toan. 1.25 = nhanh hon 25% so voi 1 opening.")]
    [Range(1f, 2f)] [SerializeField] private float twoOpeningsVentilationMultiplier = 1.25f;

    [Tooltip("Khong con leak, khong mo cua: sau thoi gian nay luong gas hien co tu giam khoang 63%.")]
    public float secondsToClearNaturally = 480f;

    [Header("Read Only")]
    [SerializeField] private bool knobLeak = false;
    [SerializeField] private bool leakPresent = false;
    [SerializeField] private bool mainSupplyOpen = false;
    [SerializeField] private int activeOpenings = 0;

    [Tooltip("Tong muc thong gio cong don tu cac vent, co the > 1")]
    public float vent01 = 0f;

    [Tooltip("Muc thong gio thuc te sau khi ap dung gioi han cong don.")]
    [SerializeField] private float effectiveVentilation01 = 0f;

    [Tooltip("Do manh leak hien tai, da tinh theo van chinh")]
    [Range(0f, 1f)] public float leakStrength01 = 0f;

    public bool leakActive = false;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private int debugGasLevel;
    [SerializeField] private float debugGasBeforeStep;
    [SerializeField] private float debugGasAfterStep;
    [SerializeField] private float debugFillRate01PerSec;
    [SerializeField] private float debugVentDrainRate01PerSec;
    [SerializeField] private float debugNaturalDrainRate01PerSec;
    [SerializeField] private float debugNetChangeRate01PerSec;
    [SerializeField] private float debugLastDeltaTime;
    [SerializeField] private bool debugFusionSpawned;
    [SerializeField] private bool debugHasStateAuthority;
    [SerializeField] private bool[] debugOpeningRegistered = new bool[4];
    [SerializeField] private float[] debugOpeningAngles = new float[4];
    [SerializeField] private float[] debugOpeningOpen01 = new float[4];

    [Tooltip("So lan SimulateGas chay trong cua so do gan nhat (khoang 0.5 giay thuc).")]
    [SerializeField] private int debugSimulationStepsPerSample;

    [Tooltip("Tong deltaTime mo phong trong cua so do gan nhat.")]
    [SerializeField] private float debugSimulatedSecondsPerSample;

    [Tooltip("Thoi gian thuc cua cua so do gan nhat.")]
    [SerializeField] private float debugRealSecondsPerSample;

    [Tooltip("simulated / real. Binh thuong gan 1; lon hon 1 cho thay mo phong dang chay nhanh/lap.")]
    [SerializeField] private float debugSimulationTimeRatio;

    [SerializeField] private float debugGasAtSampleStart;
    [SerializeField] private float debugGasAtSampleEnd;

    [Header("Runtime Debug Output")]
    [Tooltip("Ghi snapshot Runtime Debug ra Unity Console/Android logcat sau moi cua so lay mau.")]
    [SerializeField] private bool debugWriteToConsole = false;

    [Tooltip("Chi ghi Console khi co opening dang tao thong gio.")]
    [SerializeField] private bool debugConsoleOnlyWhileVentilating = true;

    public bool HoseLeak => hoseLeak;
    public bool KnobLeak => knobLeak;
    public bool LeakPresent => leakPresent;
    public bool MainSupplyOpen => mainSupplyOpen;
    public bool LeakActive => leakActive;
    public bool HasGasInRoom => gas01 > 0.001f;
    public float AcceptedMainValveOpen01 => mainValveOpen01;
    public float EffectiveVentilation01 => effectiveVentilation01;

    public event Action<int> GasLevelChanged;
    public int CurrentGasLevel => currentGasLevel;

    public bool IsFusionSpawned => fusionSpawned;
    public bool HasGasAuthority => !fusionSpawned || Object.HasStateAuthority;

    private int currentGasLevel = -1;
    private bool fusionSpawned = false;
    private bool previousLeakActive = false;
    private readonly float[] openingAngleCache = new float[4];
    private readonly bool[] openingRegistered = new bool[4];
    private readonly bool[] openingIsWindow = new bool[4];
    private readonly bool[] openingWasOpen = new bool[4];
    private const float RuntimeDebugSampleSeconds = 0.5f;
    private int debugAccumulatedSimulationSteps;
    private float debugAccumulatedSimulatedSeconds;
    private float debugSampleStartRealTime;
    private float debugAccumulatedGasStart;

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
            // Authority uses inspector/local values as the initial network truth.
            gas01 = Mathf.Clamp01(gas01);
            mainValveOpen01 = Mathf.Clamp01(mainValveOpen01);
            UpdateKnobLeak();
            UpdateVentilation();
            UpdateDerivedLeakState();
            WriteNetworkOpeningState();
            WriteNetworkGasState();
        }
        else
        {
            // Clients cache the accepted replicated values. They do not simulate gas.
            ApplyNetworkGasStateToCache();
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

        // In multiplayer, non-authority clients only mirror accepted replicated state.
        if (!Object.HasStateAuthority)
        {
            ApplyNetworkGasStateToCache();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        SimulateGas(Runner.DeltaTime, writeToNetwork: true);
    }

    private void SimulateGas(float deltaTime, bool writeToNetwork)
    {
        float gasBeforeSimulation = gas01;

        UpdateKnobLeak();
        UpdateVentilation();
        UpdateDerivedLeakState();
        CheckLeakEvent();

        float fillRate01PerSec = leakActive
            ? leakStrength01 / Mathf.Max(0.01f, secondsToFullAtMaxLeak)
            : 0f;

        float totalOpening01 = Mathf.Max(0f, vent01);
        float firstOpening01 = Mathf.Min(totalOpening01, 1f);
        float secondOpening01 = Mathf.Clamp01(totalOpening01 - 1f);

        // The first opening contributes 1.0x. The second opening gradually adds
        // only the remaining amount needed to reach the configured multiplier.
        effectiveVentilation01 = firstOpening01 +
            secondOpening01 * (twoOpeningsVentilationMultiplier - 1f);

        // Linear drain gives an exact clear time: 1 / 40 = 0.025 gas per second.
        float ventDrainRate01PerSec = effectiveVentilation01 > 0f
            ? effectiveVentilation01 / Mathf.Max(0.01f, secondsToClearWithFullVent)
            : 0f;

        // Do not add natural dissipation while a vent is active; otherwise the
        // configured 40-second clear time would become shorter than requested.
        float naturalDrainRate01PerSec = !leakActive && effectiveVentilation01 <= 0f
            ? gas01 / Mathf.Max(0.01f, secondsToClearNaturally)
            : 0f;

        gas01 += (fillRate01PerSec - ventDrainRate01PerSec - naturalDrainRate01PerSec) * deltaTime;
        gas01 = Mathf.Clamp01(gas01);

        if (!leakActive && gas01 < 0.001f)
            gas01 = 0f;

        UpdateRuntimeDebug(
            gasBeforeSimulation,
            gas01,
            deltaTime,
            fillRate01PerSec,
            ventDrainRate01PerSec,
            naturalDrainRate01PerSec);

        if (writeToNetwork)
            WriteNetworkGasState();

        RefreshGasLevel();
    }

    private void UpdateRuntimeDebug(
        float gasBefore,
        float gasAfter,
        float deltaTime,
        float fillRate,
        float ventDrainRate,
        float naturalDrainRate)
    {
        debugGasLevel = GasLevel();
        debugGasBeforeStep = gasBefore;
        debugGasAfterStep = gasAfter;
        debugFillRate01PerSec = fillRate;
        debugVentDrainRate01PerSec = ventDrainRate;
        debugNaturalDrainRate01PerSec = naturalDrainRate;
        debugNetChangeRate01PerSec = fillRate - ventDrainRate - naturalDrainRate;
        debugLastDeltaTime = deltaTime;
        debugFusionSpawned = fusionSpawned;
        debugHasStateAuthority = !fusionSpawned || (Object != null && Object.HasStateAuthority);

        EnsureRuntimeDebugArrays();
        for (int i = 0; i < openingRegistered.Length; i++)
        {
            debugOpeningRegistered[i] = openingRegistered[i];
            debugOpeningAngles[i] = openingAngleCache[i];
            debugOpeningOpen01[i] = GetOpeningOpen01(i);
        }

        float now = Time.unscaledTime;
        if (debugAccumulatedSimulationSteps == 0)
        {
            debugAccumulatedGasStart = gasBefore;
            debugSampleStartRealTime = now;
        }

        debugAccumulatedSimulationSteps++;
        debugAccumulatedSimulatedSeconds += Mathf.Max(0f, deltaTime);

        float realSeconds = Mathf.Max(0f, now - debugSampleStartRealTime);
        if (realSeconds < RuntimeDebugSampleSeconds)
            return;

        debugSimulationStepsPerSample = debugAccumulatedSimulationSteps;
        debugSimulatedSecondsPerSample = debugAccumulatedSimulatedSeconds;
        debugRealSecondsPerSample = realSeconds;
        debugSimulationTimeRatio = realSeconds > 0.0001f
            ? debugAccumulatedSimulatedSeconds / realSeconds
            : 0f;
        debugGasAtSampleStart = debugAccumulatedGasStart;
        debugGasAtSampleEnd = gasAfter;

        debugAccumulatedSimulationSteps = 0;
        debugAccumulatedSimulatedSeconds = 0f;

        WriteRuntimeDebugToConsole();
    }

    private void EnsureRuntimeDebugArrays()
    {
        if (debugOpeningRegistered == null || debugOpeningRegistered.Length != 4)
            debugOpeningRegistered = new bool[4];

        if (debugOpeningAngles == null || debugOpeningAngles.Length != 4)
            debugOpeningAngles = new float[4];

        if (debugOpeningOpen01 == null || debugOpeningOpen01.Length != 4)
            debugOpeningOpen01 = new float[4];
    }

    private void WriteRuntimeDebugToConsole()
    {
        if (!debugWriteToConsole)
            return;

        if (fusionSpawned && (Object == null || !Object.HasStateAuthority))
            return;

        if (debugConsoleOnlyWhileVentilating && effectiveVentilation01 <= 0f)
            return;

        Debug.Log(
            $"[GasVentDebug] gas={debugGasAtSampleStart:F4}->{debugGasAtSampleEnd:F4} " +
            $"level={debugGasLevel} rawVent={vent01:F3} effectiveVent={effectiveVentilation01:F3} " +
            $"activeOpenings={activeOpenings} " +
            $"rates(fill={debugFillRate01PerSec:F4}/s, vent=-{debugVentDrainRate01PerSec:F4}/s, " +
            $"natural=-{debugNaturalDrainRate01PerSec:F4}/s, net={debugNetChangeRate01PerSec:F4}/s) " +
            $"steps={debugSimulationStepsPerSample} simulated={debugSimulatedSecondsPerSample:F3}s " +
            $"real={debugRealSecondsPerSample:F3}s ratio={debugSimulationTimeRatio:F3} " +
            $"fusion={debugFusionSpawned} authority={debugHasStateAuthority} " +
            $"slots=[0:{FormatOpeningDebug(0)}, 1:{FormatOpeningDebug(1)}, " +
            $"2:{FormatOpeningDebug(2)}, 3:{FormatOpeningDebug(3)}] " +
            $"leakActive={leakActive} leakStrength={leakStrength01:F3}",
            this);
    }

    private string FormatOpeningDebug(int slot)
    {
        if (slot < 0 || slot >= debugOpeningRegistered.Length || !debugOpeningRegistered[slot])
            return "off";

        char type = openingIsWindow[slot] ? 'W' : 'D';
        return $"{type},angle={debugOpeningAngles[slot]:F1},open01={debugOpeningOpen01[slot]:F2}";
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

    private void WriteNetworkGasState()
    {
        if (!fusionSpawned || !Object.HasStateAuthority)
            return;

        Gas01Net = Mathf.Clamp01(gas01);
        MainValveOpen01Net = Mathf.Clamp01(mainValveOpen01);
        HoseLeakNet = hoseLeak;
        LeakStrength01Net = Mathf.Clamp01(leakStrength01);
        LeakActiveNet = leakActive;
    }

    private void WriteNetworkOpeningState()
    {
        if (!fusionSpawned || !Object.HasStateAuthority)
            return;

        OpeningAngle0Net = openingAngleCache[0];
        OpeningAngle1Net = openingAngleCache[1];
        OpeningAngle2Net = openingAngleCache[2];
        OpeningAngle3Net = openingAngleCache[3];
    }

    private void ApplyNetworkGasStateToCache()
    {
        gas01 = Mathf.Clamp01(Gas01Net);
        mainValveOpen01 = Mathf.Clamp01(MainValveOpen01Net);
        hoseLeak = HoseLeakNet;
        leakStrength01 = Mathf.Clamp01(LeakStrength01Net);
        leakActive = LeakActiveNet;
        leakPresent = hoseLeak || leakActive;
        mainSupplyOpen = mainValveOpen01 > mainValveOpenThreshold;
        openingAngleCache[0] = OpeningAngle0Net;
        openingAngleCache[1] = OpeningAngle1Net;
        openingAngleCache[2] = OpeningAngle2Net;
        openingAngleCache[3] = OpeningAngle3Net;
        RefreshGasLevel();
    }

    private void OnGasStateNetChanged()
    {
        // Keep public cached fields safe for local UI, visuals, and old scripts.
        ApplyNetworkGasStateToCache();
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

        bool hasSynchronizedOpenings = false;
        for (int i = 0; i < openingRegistered.Length; i++)
        {
            if (!openingRegistered[i]) continue;

            hasSynchronizedOpenings = true;
            float open01 = GetOpeningOpen01(i);
            ventSum += open01;

            if (open01 > 0f)
                activeOpenings++;
        }

        if (hasSynchronizedOpenings)
        {
            vent01 = Mathf.Max(0f, ventSum);
            return;
        }

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

    public void RegisterOpening(int slot, bool isWindow, float initialAngle)
    {
        if (!IsValidOpeningSlot(slot)) return;

        openingRegistered[slot] = true;
        openingIsWindow[slot] = isWindow;

        if (!fusionSpawned || Object.HasStateAuthority)
        {
            openingAngleCache[slot] = NormalizeOpeningAngle(initialAngle);
            openingWasOpen[slot] = IsOpeningAngleOpen(openingAngleCache[slot]);
            WriteNetworkOpeningState();
        }
    }

    public float GetOpeningAngle(int slot)
    {
        return IsValidOpeningSlot(slot) ? openingAngleCache[slot] : 0f;
    }

    public float GetOpeningOpen01(int slot)
    {
        if (!IsValidOpeningSlot(slot) || !openingRegistered[slot]) return 0f;

        return Mathf.InverseLerp(
            synchronizedOpeningActiveAngle,
            synchronizedOpeningFullAngle,
            Mathf.Abs(openingAngleCache[slot]));
    }

    public float WindowOpen01()
    {
        float total = 0f;

        for (int i = 0; i < openingRegistered.Length; i++)
        {
            if (openingRegistered[i] && openingIsWindow[i])
                total += GetOpeningOpen01(i);
        }

        return total;
    }

    public bool AnyWindowOpen()
    {
        return WindowOpen01() > 0f;
    }

    public void SetOpeningAngle(int slot, float angle, bool isWindow)
    {
        SetOpeningAngle(slot, angle, isWindow, null);
    }

    public void SetOpeningAngle(
        int slot,
        float angle,
        bool isWindow,
        string actorId)
    {
        if (!IsValidOpeningSlot(slot)) return;

        angle = NormalizeOpeningAngle(angle);

        if (!fusionSpawned)
        {
            ApplyOpeningAngle(
                slot,
                angle,
                isWindow,
                string.IsNullOrWhiteSpace(actorId) ? "LocalPlayer" : actorId);
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyOpeningAngle(
                slot,
                angle,
                isWindow,
                string.IsNullOrWhiteSpace(actorId)
                    ? GameplayEventActorId.FromRunner(Runner)
                    : actorId);
        }
        else
            RPC_RequestSetOpeningAngle(slot, angle, isWindow);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetOpeningAngle(
        int slot,
        float angle,
        bool isWindow,
        RpcInfo info = default)
    {
        ApplyOpeningAngle(
            slot,
            angle,
            isWindow,
            GameplayEventActorId.FromPlayerRef(info.Source));
    }

    private void ApplyOpeningAngle(
        int slot,
        float angle,
        bool isWindow,
        string actorId)
    {
        if (!IsValidOpeningSlot(slot)) return;

        openingRegistered[slot] = true;
        openingIsWindow[slot] = isWindow;
        openingAngleCache[slot] = NormalizeOpeningAngle(angle);

        bool isOpen = IsOpeningAngleOpen(openingAngleCache[slot]);
        if (openingWasOpen[slot] != isOpen)
        {
            openingWasOpen[slot] = isOpen;

            GameplayEventBus.Raise(
                isWindow
                    ? (isOpen ? GameplayEventType.WindowOpened : GameplayEventType.WindowClosed)
                    : (isOpen ? GameplayEventType.DoorOpened : GameplayEventType.DoorClosed),
                actorId: string.IsNullOrWhiteSpace(actorId) ? "Player" : actorId,
                targetId: $"Opening_{slot}",
                payload: openingAngleCache[slot]);
        }

        UpdateVentilation();
        WriteNetworkOpeningState();
    }

    private bool IsOpeningAngleOpen(float angle)
    {
        return Mathf.Abs(angle) >= synchronizedOpeningActiveAngle;
    }

    private static bool IsValidOpeningSlot(int slot)
    {
        return slot >= 0 && slot < 4;
    }

    private static float NormalizeOpeningAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
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
            ApplyMainValveOpen01(value);
        }
        else
        {
            RPC_RequestSetMainValveOpen01(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetMainValveOpen01(float value, RpcInfo info = default)
    {
        ApplyMainValveOpen01(value);
    }

    private void ApplyMainValveOpen01(float value)
    {
        mainValveOpen01 = Mathf.Clamp01(value);
        UpdateDerivedLeakState();
        CheckLeakEvent();
        WriteNetworkGasState();
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
            ApplyHoseLeak(value);
        }
        else
        {
            RPC_RequestSetHoseLeak(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetHoseLeak(bool value)
    {
        ApplyHoseLeak(value);
    }

    private void ApplyHoseLeak(bool value)
    {
        hoseLeak = value;
        UpdateDerivedLeakState();
        CheckLeakEvent();
        WriteNetworkGasState();
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
            RefreshGasLevel();
            WriteNetworkGasState();
        }
        else
        {
            Debug.LogWarning("Non-authority clients cannot set authoritative gas concentration.", this);
        }
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
        {
            GasLevelChanged?.Invoke(currentGasLevel);

            GameplayEventBus.Raise(
                GameplayEventType.GasLevelChanged,
                actorId: "GasSystem",
                targetId: gameObject.name,
                payload: currentGasLevel);
        }
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
        return GasLevel() >= 2 && HasGasInRoom;
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

        twoOpeningsVentilationMultiplier = Mathf.Clamp(
            twoOpeningsVentilationMultiplier,
            1f,
            2f);

        if (secondsToClearNaturally < 0.01f)
            secondsToClearNaturally = 0.01f;

        synchronizedOpeningActiveAngle = Mathf.Clamp(synchronizedOpeningActiveAngle, 0.1f, 179f);
        synchronizedOpeningFullAngle = Mathf.Clamp(
            synchronizedOpeningFullAngle,
            synchronizedOpeningActiveAngle + 0.1f,
            180f);

        level1Threshold = Mathf.Clamp(level1Threshold, 0f, 0.98f);
        level2Threshold = Mathf.Clamp(level2Threshold, level1Threshold + 0.01f, 0.99f);
        level3Threshold = Mathf.Clamp(level3Threshold, level2Threshold + 0.01f, 1f);
    }
}
