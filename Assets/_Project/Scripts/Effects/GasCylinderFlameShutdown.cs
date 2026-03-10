using UnityEngine;

public class GasCylinderFlameShutdown : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    [SerializeField] private FlameNode flameNode;
    [SerializeField] private Transform valveHandle;
    [SerializeField] private GasSystem gasSystem;

    [Header("Valve Read")]
    [SerializeField] private Axis localAxis = Axis.Y;

    [Tooltip("Goc dong van. Theo hinh cua ban hien tai co the la -45")]
    [SerializeField] private float closedAngle = -45f;

    [Tooltip("Lech trong khoang nay thi xem nhu da khoa van")]
    [SerializeField] private float shutoffToleranceDeg = 8f;

    [Header("Behavior")]
    [SerializeField] private float extinguishDelay = 0.2f;
    [SerializeField] private bool stopLeakToo = true;
    [SerializeField] private bool disableSpreadToo = true;
    [SerializeField] private bool debugLog = false;

    private float shutoffTimer;
    private bool alreadyExtinguished;

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();

        if (!gasSystem)
            gasSystem = FindFirstObjectByType<GasSystem>();
    }

    private void Update()
    {
        if (alreadyExtinguished) return;
        if (flameNode == null || valveHandle == null) return;
        if (!flameNode.IsBurning) return;

        bool valveClosed = IsValveClosedEnough();

        if (valveClosed)
        {
            shutoffTimer += Time.deltaTime;

            if (disableSpreadToo)
                flameNode.SetCanSpread(false);

            if (stopLeakToo && gasSystem != null)
                gasSystem.leakActive = false;

            if (debugLog)
                Debug.Log($"Valve near closed... timer = {shutoffTimer:0.00}");

            if (shutoffTimer >= extinguishDelay)
            {
                flameNode.Extinguish();
                alreadyExtinguished = true;

                if (debugLog)
                    Debug.Log("Gas cylinder flame extinguished by valve shutoff.");
            }
        }
        else
        {
            shutoffTimer = 0f;
        }
    }

    private bool IsValveClosedEnough()
    {
        float current = GetAxisAngle(valveHandle.localEulerAngles);
        float delta = Mathf.Abs(Mathf.DeltaAngle(current, closedAngle));
        return delta <= shutoffToleranceDeg;
    }

    private float GetAxisAngle(Vector3 euler)
    {
        return localAxis switch
        {
            Axis.X => euler.x,
            Axis.Y => euler.y,
            _ => euler.z
        };
    }
}