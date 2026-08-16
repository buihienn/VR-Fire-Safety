using System.Collections.Generic;
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
    private readonly Dictionary<GasSystem, int> overlappingGasSystems = new();
    private bool wasInGasZone;
    private bool wasInDanger;

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

        currentGasLevel = currentGas ? currentGas.GasLevel() : 0;

        bool inGasZone =
            insideGasZone &&
            currentGas != null &&
            currentGasLevel >= 1;

        if (inGasZone != wasInGasZone)
        {
            GameplayEventBus.Raise(
                inGasZone
                    ? GameplayEventType.PlayerEnteredGasZone
                    : GameplayEventType.PlayerExitedGasZone,
                actorId: GetActorId(),
                targetId: currentGas != null ? currentGas.gameObject.name : "GasZone",
                payload: currentGasLevel);

            wasInGasZone = inGasZone;
        }

        if (fainted)
            return;

        bool inDanger =
            insideGasZone &&
            currentGas != null &&
            currentGasLevel >= dangerousLevelStartsAt;

        if (inDanger != wasInDanger)
        {
            GameplayEventBus.Raise(
                inDanger
                    ? GameplayEventType.PlayerEnteredDangerZone
                    : GameplayEventType.PlayerExitedDangerZone,
                actorId: GetActorId(),
                targetId: currentGas != null ? currentGas.gameObject.name : "GasZone",
                payload: currentGasLevel);

            wasInDanger = inDanger;
        }

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

        overlappingGasSystems.TryGetValue(gas, out int overlapCount);
        overlappingGasSystems[gas] = overlapCount + 1;
        currentGas = gas;
        insideGasZone = overlappingGasSystems.Count > 0;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!ShouldRunExposure()) return;

        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        if (!overlappingGasSystems.ContainsKey(gas))
            overlappingGasSystems[gas] = 1;

        currentGas = gas;
        insideGasZone = overlappingGasSystems.Count > 0;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!ShouldRunExposure()) return;

        GasSystem gas = other.GetComponentInParent<GasSystem>();
        if (gas == null) return;

        bool wasInsideAnyGasVolume = overlappingGasSystems.Count > 0;
        int gasLevelAtExit = gas.GasLevel();

        if (overlappingGasSystems.TryGetValue(gas, out int overlapCount))
        {
            overlapCount--;
            if (overlapCount <= 0)
                overlappingGasSystems.Remove(gas);
            else
                overlappingGasSystems[gas] = overlapCount;
        }

        insideGasZone = overlappingGasSystems.Count > 0;

        // PlayerExitedGasZone in Update can also occur when gas dissipates while
        // the player is standing still. This separate event is raised only when
        // the player physically leaves the final overlapping gas volume.
        if (wasInsideAnyGasVolume && !insideGasZone && gasLevelAtExit >= 1)
        {
            GameplayEventBus.Raise(
                GameplayEventType.PlayerMovedOutOfGasZone,
                actorId: GetActorId(),
                targetId: gas.gameObject.name,
                payload: gasLevelAtExit);
        }

        if (gas == currentGas)
        {
            currentGas = null;
            foreach (GasSystem remainingGas in overlappingGasSystems.Keys)
            {
                currentGas = remainingGas;
                break;
            }
        }
    }

    private void Faint()
    {
        if (fainted) return;

        fainted = true;
        faintProgress01 = 1f;

        GameplayEventBus.Raise(
            GameplayEventType.PlayerFainted,
            actorId: GetActorId(),
            targetId: currentGas != null ? currentGas.gameObject.name : "GasZone",
            payload: currentGasLevel);

        // Record and score the terminal action before GameFlow closes scoring.
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
        overlappingGasSystems.Clear();
        currentGas = null;
        currentGasLevel = 0;
        faintProgress01 = 0f;
        fainted = false;
        wasInGasZone = false;
        wasInDanger = false;
    }

    public bool HasFainted() => fainted;
    public float GetFaintProgress01() => faintProgress01;
    public int GetCurrentGasLevel() => currentGasLevel;
}
