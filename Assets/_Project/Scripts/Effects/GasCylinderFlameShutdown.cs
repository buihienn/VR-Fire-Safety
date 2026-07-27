using System.Collections;
using UnityEngine;

public class GasCylinderFlameShutdown : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlameNode flameNode;
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private Transform valveHandle;

    [Header("Valve Closed Detection")]
    [SerializeField] private float closedAngle = -45f;
    [SerializeField] private float shutoffToleranceDeg = 8f;

    [Header("Shutdown")]
    [SerializeField] private float extinguishDelay = 0.25f;
    [SerializeField] private bool stopLeakToo = true;
    [SerializeField] private bool disableSpreadWhenClosed = false;

    [Header("Re-Ignite")]
    [SerializeField] private float ignitionRadius = 4f;
    [SerializeField] private float reigniteDelay = 2f;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float minNeighborBurn01 = 0.05f;

    [Header("Audio")]
    [SerializeField] private string gasLeakLoopSound = "GasLeakLoop";
    [SerializeField] private string gasBurstSound = "GasBurst";

    [Header("Debug Read Only")]
    [SerializeField] private bool debugValveClosed;
    [SerializeField] private bool debugGasStillLeaking;
    [SerializeField] private bool debugNearbyFireFound;
    [SerializeField] private float debugReigniteTimer;
    [SerializeField] private bool debugSequenceIgnitionLocked;

    private Coroutine extinguishRoutine;
    private float reigniteTimer = 0f;
    private float nextCheckTime = 0f;
    private bool sequenceIgnitionLocked;

    private const float ForceExtinguishAmount = 999999f;

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();

        if (!gasSystem)
            gasSystem = GasSystem.Instance;
    }

    private void Update()
    {
        if (flameNode == null)
            return;
        
        if (FireManager.Instance != null && !FireManager.Instance.HasFireAuthority)
            return;

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        bool valveClosed = IsValveClosed();
        debugValveClosed = valveClosed;

        if (valveClosed)
        {
            HandleValveClosedState();
            return;
        }

        HandleValveOpenState();
    }

    private void HandleValveClosedState()
    {
        ResetReigniteTimer();

        debugGasStillLeaking = false;
        debugNearbyFireFound = false;

        if (stopLeakToo && gasSystem != null)
            gasSystem.SetMainValveOpen01(0f);

        if (disableSpreadWhenClosed)
            flameNode.SetCanSpread(false);

        SetGasLeakLoop(false);

        if (IsNodeBurning(flameNode) && extinguishRoutine == null)
            extinguishRoutine = StartCoroutine(ExtinguishAfterDelay());
    }

    private void HandleValveOpenState()
    {
        if (disableSpreadWhenClosed)
            flameNode.SetCanSpread(true);

        CancelExtinguishRoutine();

        // The Gas Unit has an explicit hose -> after-fire -> valve sequence.
        // While that sequence owns the valve, nearby fires must not bypass it.
        if (sequenceIgnitionLocked)
        {
            debugGasStillLeaking = false;
            debugNearbyFireFound = false;
            ResetReigniteTimer();
            return;
        }

        bool gasStillLeaking = gasSystem != null && gasSystem.CanSustainNozzleFire();
        bool nearbyFireFound = HasNearbyBurningNode();
        bool flameBurning = IsNodeBurning(flameNode);

        debugGasStillLeaking = gasStillLeaking;
        debugNearbyFireFound = nearbyFireFound;

        bool shouldPlayLeakLoop = gasStillLeaking && !flameBurning;
        SetGasLeakLoop(shouldPlayLeakLoop);

        if (flameBurning)
        {
            ResetReigniteTimer();
            return;
        }

        if (!gasStillLeaking || !nearbyFireFound)
        {
            ResetReigniteTimer();
            return;
        }

        reigniteTimer += checkInterval;
        debugReigniteTimer = reigniteTimer;

        if (reigniteTimer < reigniteDelay)
            return;

        SetGasLeakLoop(false);
        IgniteNode(flameNode);
        PlayOneShot(gasBurstSound);

        ResetReigniteTimer();
    }

    /// <summary>
    /// Prevents the valve flame from using its proximity re-ignite path.
    /// HoseBurnSequence releases this lock only after the hose fire stages finish.
    /// Valve-close shutdown remains active while locked.
    /// </summary>
    public void SetSequenceIgnitionLocked(bool locked)
    {
        sequenceIgnitionLocked = locked;
        debugSequenceIgnitionLocked = locked;
        ResetReigniteTimer();

        if (locked)
            SetGasLeakLoop(false);
    }

    private void IgniteNode(FlameNode node)
    {
        if (node == null) return;

        node.gameObject.SetActive(true);

        if (FireManager.Instance != null)
            FireManager.Instance.RequestIgnite(node);
        else
            node.Ignite();
    }

    private void ExtinguishNode(FlameNode node)
    {
        if (node == null) return;

        if (FireManager.Instance != null)
            FireManager.Instance.RequestExtinguish(node, ForceExtinguishAmount);
        else
            node.Extinguish();
    }

    private bool IsNodeBurning(FlameNode node)
    {
        if (node == null) return false;

        if (FireManager.Instance != null)
            return FireManager.Instance.IsNodeBurning(node);

        return node.IsBurning;
    }

    private void ResetReigniteTimer()
    {
        reigniteTimer = 0f;
        debugReigniteTimer = 0f;
    }

    private void CancelExtinguishRoutine()
    {
        if (extinguishRoutine == null)
            return;

        StopCoroutine(extinguishRoutine);
        extinguishRoutine = null;
    }

    private void SetGasLeakLoop(bool shouldPlay)
    {
        if (AudioManager.Instance == null)
            return;

        if (shouldPlay)
        {
            if (!AudioManager.Instance.IsPlaying(gasLeakLoopSound))
                AudioManager.Instance.Play(gasLeakLoopSound);
        }
        else
        {
            if (AudioManager.Instance.IsPlaying(gasLeakLoopSound))
                AudioManager.Instance.Stop(gasLeakLoopSound);
        }
    }

    private void PlayOneShot(string soundName)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayOneShot(soundName);
    }

    private IEnumerator ExtinguishAfterDelay()
    {
        if (extinguishDelay > 0f)
            yield return new WaitForSeconds(extinguishDelay);

        ExtinguishNode(flameNode);

        extinguishRoutine = null;
    }

    private bool IsValveClosed()
    {
        if (valveHandle == null)
            return gasSystem != null && !gasSystem.MainSupplyOpen;

        float currentAngle = Normalize180(valveHandle.localEulerAngles.y);
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, closedAngle));
        return delta <= shutoffToleranceDeg;
    }

    private bool HasNearbyBurningNode()
    {
        if (flameNode == null)
            return false;

        float radiusSqr = ignitionRadius * ignitionRadius;
        Vector3 myPos = flameNode.transform.position;

        foreach (FlameNode node in FlameNode.All)
        {
            if (node == null) continue;
            if (node == flameNode) continue;
            if (!IsNodeBurning(node)) continue;
            if (node.Burn01 < minNeighborBurn01) continue;

            Vector3 delta = node.transform.position - myPos;
            if (delta.sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    private float Normalize180(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, ignitionRadius);
    }
}
