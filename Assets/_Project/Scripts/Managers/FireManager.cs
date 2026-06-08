using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class FireManager : NetworkBehaviour
{
    public static FireManager Instance { get; private set; }

    [Header("Flame Nodes")]
    [SerializeField] private List<FlameNode> flameNodes = new();

    [Tooltip("Nếu Flame Nodes list đang trống, FireManager sẽ tự tìm FlameNode trong toàn scene.")]
    [SerializeField] private bool autoFindFlameNodesInSceneIfListEmpty = true;

    [Tooltip("Nếu auto find, sort theo FlameId để Host/Client có thứ tự ổn định hơn.")]
    [SerializeField] private bool sortAutoFoundNodesById = true;

    [Header("Network / Fallback")]
    [Tooltip("Cho phép chạy local nếu FireManager chưa được Fusion Spawned. Hữu ích cho test single-player. Trong multiplayer thật, nên ignite sau khi network ready.")]
    [SerializeField] private bool allowLocalFallbackBeforeFusionSpawned = true;

    [Header("Spread")]
    [SerializeField] private bool allowSpread = true;

    [Header("Debug")]
    [SerializeField] private bool logFireEvents = true;

    private bool fusionSpawned;
    private Coroutine[] spreadIgniteRoutines;

    public bool IsFusionSpawned => fusionSpawned;

    public bool HasFireAuthority
    {
        get
        {
            if (!fusionSpawned)
                return allowLocalFallbackBeforeFusionSpawned;

            return Object.HasStateAuthority;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one FireManager found. Destroying duplicate.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        AutoFindNodesIfNeeded();
        ValidateDuplicateFlameIds();
        InitArrays();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        AutoFindNodesIfNeeded();
        ValidateDuplicateFlameIds();
        InitArrays();

        if (!Object.HasStateAuthority)
            return;

        // Host đọc trạng thái ban đầu và sync lại cho tất cả client.
        for (int i = 0; i < flameNodes.Count; i++)
        {
            FlameNode node = flameNodes[i];
            if (node == null) continue;

            bool startBurning = node.IsBurning;
            float health01 = startBurning ? Mathf.Max(node.Health01, 1f) : 0f;

            SyncNodeState(i, startBurning, health01);
        }
    }

    private void AutoFindNodesIfNeeded()
    {
        if (!autoFindFlameNodesInSceneIfListEmpty) return;
        if (flameNodes != null && flameNodes.Count > 0) return;

        FlameNode[] found = UnityEngine.Object.FindObjectsByType<FlameNode>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        flameNodes = new List<FlameNode>();

        foreach (FlameNode node in found)
        {
            if (node == null) continue;
            if (!node.gameObject.scene.IsValid()) continue;

            flameNodes.Add(node);
        }

        if (sortAutoFoundNodesById)
        {
            flameNodes.Sort((a, b) =>
                string.Compare(a.FlameId, b.FlameId, StringComparison.Ordinal));
        }
    }

    private void InitArrays()
    {
        int count = flameNodes != null ? flameNodes.Count : 0;
        spreadIgniteRoutines = new Coroutine[count];
    }

    private void ValidateDuplicateFlameIds()
    {
        if (flameNodes == null) return;

        HashSet<string> ids = new HashSet<string>();

        foreach (FlameNode node in flameNodes)
        {
            if (node == null) continue;

            string id = node.FlameId;
            if (!ids.Add(id))
            {
                Debug.LogError(
                    $"[FireManager] Duplicate FlameId found: {id}. Rename the FlameNode object or set a unique Flame Id.",
                    node
                );
            }
        }
    }

    // =========================
    // IGNITE / SPREAD
    // =========================

    public void RequestIgnite(FlameNode node, float delay = 0f)
    {
        if (node == null) return;

        int nodeIndex = GetNodeIndex(node);
        if (nodeIndex < 0)
        {
            Debug.LogWarning($"[FireManager] RequestIgnite ignored. Node is not registered: {node.name}", node);
            return;
        }

        if (!fusionSpawned)
        {
            if (!allowLocalFallbackBeforeFusionSpawned) return;
            StartCoroutine(IgniteAfterDelayLocal(nodeIndex, delay, "Local"));
            return;
        }

        if (Object.HasStateAuthority)
        {
            StartCoroutine(IgniteAfterDelayLocal(nodeIndex, delay, PlayerToActorId(Runner.LocalPlayer)));
        }
        else
        {
            RPC_RequestIgnite(nodeIndex, delay);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestIgnite(int nodeIndex, float delay, RpcInfo info = default)
    {
        StartCoroutine(IgniteAfterDelayLocal(nodeIndex, delay, PlayerToActorId(info.Source)));
    }

    private IEnumerator IgniteAfterDelayLocal(int nodeIndex, float delay, string actorId)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        IgniteNodeByIndex(nodeIndex, actorId);
    }

    public void RequestSpreadIgnite(FlameNode sourceNode, FlameNode targetNode, float delay)
    {
        if (!allowSpread) return;
        if (sourceNode == null || targetNode == null) return;
        if (!sourceNode.CanSpread) return;
        if (!sourceNode.IsBurning) return;
        if (targetNode.IsBurning) return;
        if (!targetNode.AllowIgniteFromSpread) return;
        if (targetNode.SpreadReigniteLocked) return;

        int sourceIndex = GetNodeIndex(sourceNode);
        int targetIndex = GetNodeIndex(targetNode);

        if (!IsValidNode(sourceIndex)) return;
        if (!IsValidNode(targetIndex)) return;

        if (!fusionSpawned)
        {
            if (!allowLocalFallbackBeforeFusionSpawned) return;
            StartSpreadIgniteRoutine(sourceIndex, targetIndex, delay);
            return;
        }

        if (!Object.HasStateAuthority)
            return;

        StartSpreadIgniteRoutine(sourceIndex, targetIndex, delay);
    }

    private void StartSpreadIgniteRoutine(int sourceIndex, int targetIndex, float delay)
    {
        StopSpreadIgniteRoutine(targetIndex);
        spreadIgniteRoutines[targetIndex] = StartCoroutine(
            SpreadIgniteRoutine(sourceIndex, targetIndex, delay)
        );
    }

    private IEnumerator SpreadIgniteRoutine(int sourceIndex, int targetIndex, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!allowSpread) yield break;
        if (!IsValidNode(sourceIndex)) yield break;
        if (!IsValidNode(targetIndex)) yield break;

        FlameNode sourceNode = flameNodes[sourceIndex];
        FlameNode targetNode = flameNodes[targetIndex];

        if (sourceNode == null || targetNode == null) yield break;
        if (!sourceNode.CanSpread) yield break;
        if (!sourceNode.IsBurning) yield break;
        if (targetNode.IsBurning) yield break;
        if (!targetNode.AllowIgniteFromSpread) yield break;
        if (targetNode.SpreadReigniteLocked) yield break;

        IgniteNodeByIndex(targetIndex, sourceNode.FlameId);
        spreadIgniteRoutines[targetIndex] = null;
    }

    private void IgniteNodeByIndex(int nodeIndex, string actorId)
    {
        if (!IsValidNode(nodeIndex)) return;

        FlameNode node = flameNodes[nodeIndex];
        if (node == null) return;
        if (node.IsBurning) return;

        node.ResetHealthFromFireManager();
        SyncNodeState(nodeIndex, true, node.Health01);

        GameplayEventBus.Raise(
            GameplayEventType.FireIgnited,
            actorId,
            node.FlameId,
            nodeIndex
        );

        if (logFireEvents)
            Debug.Log($"[FireManager] Fire ignited: {node.FlameId} by {actorId}", this);
    }

    // =========================
    // EXTINGUISH FROM CLIENT/HOST
    // =========================

    public void RequestExtinguish(FlameNode node, float amount)
    {
        if (node == null) return;

        int nodeIndex = GetNodeIndex(node);
        if (nodeIndex < 0) return;

        amount = Mathf.Abs(amount);
        if (amount <= 0f) return;

        if (!fusionSpawned)
        {
            if (!allowLocalFallbackBeforeFusionSpawned) return;
            ApplyExtinguish(nodeIndex, amount, "Local");
            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyExtinguish(nodeIndex, amount, PlayerToActorId(Runner.LocalPlayer));
        }
        else
        {
            RPC_RequestExtinguish(nodeIndex, amount);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestExtinguish(int nodeIndex, float amount, RpcInfo info = default)
    {
        ApplyExtinguish(nodeIndex, amount, PlayerToActorId(info.Source));
    }

    private void ApplyExtinguish(int nodeIndex, float amount, string actorId)
    {
        if (!IsValidNode(nodeIndex)) return;

        FlameNode node = flameNodes[nodeIndex];
        if (node == null) return;
        if (!node.IsBurning) return;

        bool extinguished = node.ApplyExtinguishFromFireManager(amount);

        if (!extinguished)
        {
            SyncNodeHealth(nodeIndex, node.Health01);
            return;
        }

        StopSpreadIgniteRoutine(nodeIndex);
        SyncNodeState(nodeIndex, false, 0f);

        GameplayEventBus.Raise(
            GameplayEventType.FireExtinguished,
            actorId,
            node.FlameId,
            nodeIndex
        );

        if (logFireEvents)
            Debug.Log($"[FireManager] {actorId} extinguished {node.FlameId}", this);
    }

    // =========================
    // SAFE SYNC WRAPPERS
    // =========================

    private void SyncNodeState(int nodeIndex, bool isBurning, float health01)
    {
        ApplyNodeStateLocal(nodeIndex, isBurning, health01);

        if (fusionSpawned)
            RPC_SetNodeState(nodeIndex, isBurning, health01);
    }

    private void SyncNodeHealth(int nodeIndex, float health01)
    {
        ApplyNodeHealthLocal(nodeIndex, health01);

        if (fusionSpawned)
            RPC_SetNodeHealth(nodeIndex, health01);
    }

    private void ApplyNodeStateLocal(int nodeIndex, bool isBurning, float health01)
    {
        if (!IsValidNode(nodeIndex)) return;

        FlameNode node = flameNodes[nodeIndex];
        if (node == null) return;

        if (isBurning)
        {
            if (health01 <= 0f)
                health01 = 1f;

            node.SetHealth01FromFireManager(health01);
            node.SetBurningFromFireManager(true, HasFireAuthority);
        }
        else
        {
            node.SetHealth01FromFireManager(0f);
            node.SetBurningFromFireManager(false, false);
        }
    }

    private void ApplyNodeHealthLocal(int nodeIndex, float health01)
    {
        if (!IsValidNode(nodeIndex)) return;

        FlameNode node = flameNodes[nodeIndex];
        if (node == null) return;

        node.SetHealth01FromFireManager(health01);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetNodeState(int nodeIndex, bool isBurning, float health01)
    {
        ApplyNodeStateLocal(nodeIndex, isBurning, health01);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetNodeHealth(int nodeIndex, float health01)
    {
        ApplyNodeHealthLocal(nodeIndex, health01);
    }

    // =========================
    // PUBLIC HELPERS
    // =========================

    public bool IsNodeBurning(FlameNode node)
    {
        int index = GetNodeIndex(node);
        if (!IsValidNode(index)) return false;

        return flameNodes[index] != null && flameNodes[index].IsBurning;
    }

    public float GetNodeHealth01(FlameNode node)
    {
        int index = GetNodeIndex(node);
        if (!IsValidNode(index)) return 0f;

        return flameNodes[index] != null ? flameNodes[index].Health01 : 0f;
    }

    public void ForceExtinguishAll()
    {
        if (!HasFireAuthority) return;

        for (int i = 0; i < flameNodes.Count; i++)
        {
            if (!IsValidNode(i)) continue;
            if (flameNodes[i] == null) continue;
            if (!flameNodes[i].IsBurning) continue;

            ApplyExtinguish(i, 999999f, "Host");
        }
    }

    public void ForceIgniteAll()
    {
        if (!HasFireAuthority) return;

        for (int i = 0; i < flameNodes.Count; i++)
            IgniteNodeByIndex(i, "Host");
    }

    // =========================
    // INTERNAL UTILS
    // =========================

    private int GetNodeIndex(FlameNode node)
    {
        if (node == null || flameNodes == null)
            return -1;

        return flameNodes.IndexOf(node);
    }

    private bool IsValidNode(int index)
    {
        return flameNodes != null
            && index >= 0
            && index < flameNodes.Count;
    }

    private void StopSpreadIgniteRoutine(int nodeIndex)
    {
        if (spreadIgniteRoutines == null) return;
        if (nodeIndex < 0 || nodeIndex >= spreadIgniteRoutines.Length) return;

        if (spreadIgniteRoutines[nodeIndex] != null)
        {
            StopCoroutine(spreadIgniteRoutines[nodeIndex]);
            spreadIgniteRoutines[nodeIndex] = null;
        }
    }

    private string PlayerToActorId(PlayerRef player)
    {
        if (player == PlayerRef.None)
            return "Host";

        return $"Player_{player.PlayerId}";
    }
}
