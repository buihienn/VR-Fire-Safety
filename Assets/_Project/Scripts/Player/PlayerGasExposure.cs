using Fusion;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PlayerGasExposure : MonoBehaviour
{
    [Header("Multiplayer")]
    [SerializeField] private bool onlyRunForLocalPlayer = true;

    [Tooltip("Nếu để trống, script tự tìm NetworkObject ở parent.")]
    [SerializeField] private NetworkObject playerNetworkObject;

    [Tooltip("Bật true để khi ngất thì báo GameFlowManager. UnityEvent onFainted chỉ nên dùng cho UI/audio local.")]
    [SerializeField] private bool notifyGameFlowManager = true;

    [Header("Danger Rules")]
    [Min(1)] public int dangerousLevelStartsAt = 2;
    [Min(0.1f)] public float secondsToFaintAtLevel2 = 60f;
    [Min(0.1f)] public float secondsToFaintAtLevel3 = 40f;
    [Min(0.1f)] public float recoverySecondsFromFull = 10f;

    [Header("Events")]
    public UnityEvent onFainted;

    [Header("Debug")]
    [SerializeField] private bool isLocalExposure = true;
    [SerializeField] private bool insideGasZone;
    [SerializeField] private GasSystem currentGas;
    [SerializeField] private int currentGasLevel;
    [Range(0f, 1f)] [SerializeField] private float faintProgress01;
    [SerializeField] private bool fainted;

    private Collider exposureCollider;

    private void Awake()
    {
        exposureCollider = GetComponent<Collider>();
        exposureCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;

        if (playerNetworkObject == null)
            playerNetworkObject = GetComponentInParent<NetworkObject>();
    }

    private void Update()
    {
        isLocalExposure = ShouldRunExposure();

        if (!isLocalExposure)
            return;

        if (fainted)
            return;

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

    private bool ShouldRunExposure()
    {
        if (!onlyRunForLocalPlayer)
            return true;

        if (playerNetworkObject == null)
            playerNetworkObject = GetComponentInParent<NetworkObject>();

        // Single-player fallback.
        if (playerNetworkObject == null)
            return true;

        // Multiplayer: chỉ local player/input authority mới tự tính exposure.
        return playerNetworkObject.HasInputAuthority;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ShouldRunExposure()) return;

        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        currentGas = gas;
        insideGasZone = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!ShouldRunExposure()) return;

        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        currentGas = gas;
        insideGasZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!ShouldRunExposure()) return;

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

        if (notifyGameFlowManager && GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ReportPlayerFainted();
        }

        Debug.Log($"PLAYER FAINTED -> Report to GameFlowManager. Actor={GetActorId()}");
    }

    private string GetActorId()
    {
        if (playerNetworkObject == null)
            playerNetworkObject = GetComponentInParent<NetworkObject>();

        if (playerNetworkObject == null)
            return gameObject.name;

        PlayerRef inputAuthority = playerNetworkObject.InputAuthority;

        if (inputAuthority == PlayerRef.None)
            return "Host";

        return $"Player_{inputAuthority.PlayerId}";
    }

    public void ResetExposure()
    {
        insideGasZone = false;
        currentGas = null;
        currentGasLevel = 0;
        faintProgress01 = 0f;
        fainted = false;
    }

    public bool HasFainted() => fainted;
    public float GetFaintProgress01() => faintProgress01;
    public int GetCurrentGasLevel() => currentGasLevel;
}