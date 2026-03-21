using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PlayerGasExposure : MonoBehaviour
{
    [Header("Danger Rules")]
    [Min(1)] public int dangerousLevelStartsAt = 2;
    [Min(0.1f)] public float secondsToFaintAtLevel2 = 60f;
    [Min(0.1f)] public float secondsToFaintAtLevel3 = 40f;
    [Min(0.1f)] public float recoverySecondsFromFull = 10f;

    [Header("Events")]
    public UnityEvent onFainted;

    [Header("Debug")]
    [SerializeField] private bool insideGasZone;
    [SerializeField] private GasSystem currentGas;
    [SerializeField] private int currentGasLevel;
    [Range(0f, 1f)] [SerializeField] private float faintProgress01;
    [SerializeField] private bool fainted;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Update()
    {
        if (fainted) return;

        currentGasLevel = currentGas ? currentGas.GasLevel() : 0;

        bool inDanger =
            insideGasZone &&
            currentGas != null &&
            currentGasLevel >= dangerousLevelStartsAt;

        if (inDanger)
        {
            float secondsToFaint =
                currentGasLevel >= 3 ? secondsToFaintAtLevel3 : secondsToFaintAtLevel2;

            faintProgress01 += Time.deltaTime / Mathf.Max(0.01f, secondsToFaint);
        }
        else
        {
            faintProgress01 -= Time.deltaTime / Mathf.Max(0.01f, recoverySecondsFromFull);
        }

        faintProgress01 = Mathf.Clamp01(faintProgress01);

        if (faintProgress01 >= 1f)
            Faint();
    }

    private void OnTriggerEnter(Collider other)
    {
        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        currentGas = gas;
        insideGasZone = true;
    }

    private void OnTriggerStay(Collider other)
    {
        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        currentGas = gas;
        insideGasZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;
        if (gas != currentGas) return;

        insideGasZone = false;
        currentGas = null;
    }

    private void Faint()
    {
        if (fainted) return;

        fainted = true;
        faintProgress01 = 1f;

        onFainted?.Invoke();

        Debug.Log("PLAYER FAINTED -> GAME OVER");
    }

    public bool HasFainted() => fainted;
    public float GetFaintProgress01() => faintProgress01;
    public int GetCurrentGasLevel() => currentGasLevel;
}