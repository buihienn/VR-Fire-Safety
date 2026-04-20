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

    [Header("Debug Read Only")]
    [SerializeField] private bool debugValveClosed;
    [SerializeField] private bool debugGasStillLeaking;
    [SerializeField] private bool debugNearbyFireFound;
    [SerializeField] private float debugReigniteTimer;

    private Coroutine extinguishRoutine;
    private float reigniteTimer = 0f;
    private float nextCheckTime = 0f;

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();

        if (!gasSystem)
            gasSystem = GasSystem.Instance;
    }

    private void Update()
    {
        if (!flameNode)
            return;

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        bool valveClosed = IsValveClosed();
        debugValveClosed = valveClosed;

        if (valveClosed)
        {
            reigniteTimer = 0f;
            debugReigniteTimer = 0f;
            debugGasStillLeaking = false;
            debugNearbyFireFound = false;

            if (stopLeakToo && gasSystem != null)
                gasSystem.SetMainValveOpen01(0f);

            if (disableSpreadWhenClosed)
                flameNode.SetCanSpread(false);

            if (flameNode.IsBurning && extinguishRoutine == null)
                extinguishRoutine = StartCoroutine(ExtinguishAfterDelay());

            return;
        }

        if (disableSpreadWhenClosed)
            flameNode.SetCanSpread(true);

        if (extinguishRoutine != null)
        {
            StopCoroutine(extinguishRoutine);
            extinguishRoutine = null;
        }

        bool gasStillLeaking = gasSystem != null && gasSystem.CanSustainNozzleFire();
        bool nearbyFireFound = HasNearbyBurningNode();

        debugGasStillLeaking = gasStillLeaking;
        debugNearbyFireFound = nearbyFireFound;

        if (flameNode.IsBurning)
        {
            reigniteTimer = 0f;
            debugReigniteTimer = 0f;
            return;
        }

        if (gasStillLeaking && nearbyFireFound)
        {
            reigniteTimer += checkInterval;
            debugReigniteTimer = reigniteTimer;

            if (reigniteTimer >= reigniteDelay)
            {
                flameNode.ForceIgnite();
                reigniteTimer = 0f;
                debugReigniteTimer = 0f;
            }
        }
        else
        {
            reigniteTimer = 0f;
            debugReigniteTimer = 0f;
        }
    }

    private IEnumerator ExtinguishAfterDelay()
    {
        if (extinguishDelay > 0f)
            yield return new WaitForSeconds(extinguishDelay);

        if (flameNode != null)
            flameNode.Extinguish();

        extinguishRoutine = null;
    }

    private bool IsValveClosed()
    {
        if (valveHandle == null)
        {
            return gasSystem != null && !gasSystem.MainSupplyOpen;
        }

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
            if (!node.IsBurning) continue;
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