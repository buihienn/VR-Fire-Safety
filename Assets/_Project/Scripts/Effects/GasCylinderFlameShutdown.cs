using UnityEngine;

public class GasCylinderFlameShutdown : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    [SerializeField] private FlameNode flameNode;
    [SerializeField] private Transform valveHandle;
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private Transform reigniteOrigin;

    [Header("Valve Read")]
    [SerializeField] private Axis localAxis = Axis.Y;
    [SerializeField] private float closedAngle = -45f;
    [SerializeField] private float shutoffToleranceDeg = 8f;

    [Header("Behavior")]
    [SerializeField] private float extinguishDelay = 0.2f;
    [SerializeField] private bool stopLeakToo = true;
    [SerializeField] private bool disableSpreadToo = true;

    [Header("Reignite")]
    [SerializeField] private bool allowReignite = true;
    [SerializeField] private float reigniteDelay = 1.5f;
    [SerializeField] private float ignitionRadius = 2f;
    [SerializeField] private float minNeighborBurn01 = 0.1f;

    [Header("Debug / Read Only")]
    [SerializeField] private bool debugValveClosed;
    [SerializeField] private bool debugLeakActive;
    [SerializeField] private bool debugHasNearbyBurningFlame;
    [SerializeField] private float debugNearestBurningFlameDistance = -1f;
    [SerializeField] private float debugReigniteTimer;

    private float shutoffTimer;
    private float reigniteTimer;

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();

        if (!gasSystem)
            gasSystem = FindFirstObjectByType<GasSystem>();

        if (!reigniteOrigin)
            reigniteOrigin = transform;
    }

    private void Update()
    {
        if (flameNode == null || valveHandle == null) return;

        bool valveClosed = IsValveClosedEnough();
        debugValveClosed = valveClosed;

        if (disableSpreadToo)
            flameNode.SetCanSpread(!valveClosed);

        HandleValveShutoff(valveClosed);
        HandleReignite(valveClosed);
    }

    private void HandleValveShutoff(bool valveClosed)
    {
        if (!flameNode.IsBurning)
        {
            shutoffTimer = 0f;
            return;
        }

        if (valveClosed)
        {
            shutoffTimer += Time.deltaTime;

            if (stopLeakToo && gasSystem != null)
                gasSystem.SetMainValveOpen01(0f);

            if (shutoffTimer >= extinguishDelay)
                flameNode.Extinguish();
        }
        else
        {
            shutoffTimer = 0f;
        }
    }

    private void HandleReignite(bool valveClosed)
    {
        if (!allowReignite || flameNode == null || gasSystem == null)
        {
            ResetReigniteDebug();
            return;
        }

        debugLeakActive = gasSystem.leakActive;

        if (valveClosed)
        {
            ResetReigniteDebug();
            return;
        }

        if (flameNode.IsBurning)
        {
            ResetReigniteDebug();
            return;
        }

        if (!gasSystem.leakActive)
        {
            ResetReigniteDebug();
            return;
        }

        bool hasNearbyFlame = HasNearbyBurningFlame(out float nearestDist);
        debugHasNearbyBurningFlame = hasNearbyFlame;
        debugNearestBurningFlameDistance = nearestDist;

        if (!hasNearbyFlame)
        {
            reigniteTimer = 0f;
            debugReigniteTimer = 0f;
            return;
        }

        reigniteTimer += Time.deltaTime;
        debugReigniteTimer = reigniteTimer;

        if (reigniteTimer >= reigniteDelay)
        {
            flameNode.Ignite();
            reigniteTimer = 0f;
            debugReigniteTimer = 0f;
        }
    }

    private bool HasNearbyBurningFlame(out float nearestDistance)
    {
        nearestDistance = -1f;

        Vector3 center = reigniteOrigin ? reigniteOrigin.position : transform.position;
        float radiusSqr = ignitionRadius * ignitionRadius;
        bool found = false;

        for (int i = 0; i < FlameNode.All.Count; i++)
        {
            FlameNode other = FlameNode.All[i];
            if (other == null) continue;
            if (other == flameNode) continue;
            if (!other.IsBurning) continue;
            if (other.Burn01 < minNeighborBurn01) continue;

            float sqrDist = (other.transform.position - center).sqrMagnitude;
            float dist = Mathf.Sqrt(sqrDist);

            if (nearestDistance < 0f || dist < nearestDistance)
                nearestDistance = dist;

            if (sqrDist <= radiusSqr)
                found = true;
        }

        return found;
    }

    private void ResetReigniteDebug()
    {
        reigniteTimer = 0f;
        debugLeakActive = gasSystem != null && gasSystem.leakActive;
        debugHasNearbyBurningFlame = false;
        debugNearestBurningFlameDistance = -1f;
        debugReigniteTimer = 0f;
    }

    private bool IsValveClosedEnough()
    {
        float current = GetAxisAngle(valveHandle.localEulerAngles);
        float delta = Mathf.Abs(Mathf.DeltaAngle(current, closedAngle));
        return delta <= shutoffToleranceDeg;
    }

    private float GetAxisAngle(Vector3 euler)
    {
        float angle = localAxis switch
        {
            Axis.X => euler.x,
            Axis.Y => euler.y,
            _ => euler.z
        };

        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private void OnValidate()
    {
        if (extinguishDelay < 0f) extinguishDelay = 0f;
        if (reigniteDelay < 0f) reigniteDelay = 0f;
        if (ignitionRadius < 0.05f) ignitionRadius = 0.05f;
        if (minNeighborBurn01 < 0f) minNeighborBurn01 = 0f;
        if (minNeighborBurn01 > 1f) minNeighborBurn01 = 1f;
        if (shutoffToleranceDeg < 0f) shutoffToleranceDeg = 0f;
    }
}