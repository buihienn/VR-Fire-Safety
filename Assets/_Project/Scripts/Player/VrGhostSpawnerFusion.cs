using System.Collections;
using Fusion;
using Meta.XR.MultiplayerBlocks.Fusion;
using UnityEngine;

public class VrGhostSpawnerFusion : MonoBehaviour
{
    [Header("Prefab")]
    [NetworkPrefab]
    [SerializeField] private NetworkObject playerGhostPrefab;

    [Header("Local Tracking Sources")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private bool autoFindMissingSources = true;

    [Header("Spawn")]
    [SerializeField] private Vector3 spawnPositionOffset;
    [SerializeField] private bool setAsFusionPlayerObject = true;

    [Header("Debug")]
    [SerializeField] private NetworkObject spawnedGhost;
    [SerializeField] private string connectedRoomToken;
    [SerializeField] private int activePlayerCount;

    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        FusionBBEvents.OnSceneLoadDone += OnSceneLoadDone;
        BeginSpawnWhenReady(null);
    }

    private void OnDisable()
    {
        FusionBBEvents.OnSceneLoadDone -= OnSceneLoadDone;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void OnSceneLoadDone(NetworkRunner runner)
    {
        BeginSpawnWhenReady(runner);
    }

    private void BeginSpawnWhenReady(NetworkRunner runner)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(SpawnWhenReady(runner));
    }

    private IEnumerator SpawnWhenReady(NetworkRunner runner)
    {
        if (spawnedGhost != null)
        {
            spawnRoutine = null;
            yield break;
        }

        while (spawnedGhost == null && (runner == null || !runner.IsRunning))
        {
            runner = GetActiveRunner();
            yield return null;
        }

        while (runner == null || !runner.IsRunning || runner.LocalPlayer == PlayerRef.None)
        {
            yield return null;
        }

        if (spawnedGhost != null)
        {
            spawnRoutine = null;
            yield break;
        }

        if (playerGhostPrefab == null)
        {
            Debug.LogError($"[{nameof(VrGhostSpawnerFusion)}] Missing Player Ghost Prefab.");
            yield break;
        }

        if (autoFindMissingSources)
        {
            FindMissingTrackingSources();
        }

        connectedRoomToken = runner.SessionInfo?.Name ?? string.Empty;
        activePlayerCount = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
        {
            activePlayerCount++;
        }

        Vector3 spawnPosition = head != null ? head.position + spawnPositionOffset : spawnPositionOffset;
        Quaternion spawnRotation = head != null ? Quaternion.Euler(0f, head.eulerAngles.y, 0f) : Quaternion.identity;

        spawnedGhost = runner.Spawn(
            playerGhostPrefab,
            spawnPosition,
            spawnRotation,
            runner.LocalPlayer,
            (_, obj) =>
            {
                NetworkedVrGhost ghost = obj.GetComponent<NetworkedVrGhost>();
                if (ghost != null)
                {
                    ghost.SetLocalSources(head, leftHand, rightHand);
                }
            });

        if (setAsFusionPlayerObject && runner.GetPlayerObject(runner.LocalPlayer) == null)
        {
            runner.SetPlayerObject(runner.LocalPlayer, spawnedGhost);
        }

        Debug.Log(
            $"[{nameof(VrGhostSpawnerFusion)}] Spawned local ghost. Room={connectedRoomToken}, LocalPlayer={runner.LocalPlayer}, ActivePlayers={activePlayerCount}");

        spawnRoutine = null;
    }

    private static NetworkRunner GetActiveRunner()
    {
        for (int i = NetworkRunner.Instances.Count - 1; i >= 0; i--)
        {
            NetworkRunner runner = NetworkRunner.Instances[i];
            if (runner != null && runner.IsRunning)
            {
                return runner;
            }
        }

        return null;
    }

    private void FindMissingTrackingSources()
    {
        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;
        }

        if (leftHand == null)
        {
            leftHand = FindFirstTransformByName(
                "LeftHandAnchor",
                "LeftControllerAnchor",
                "[BuildingBlock] Controller Tracking Left",
                "[BuildingBlock] Hand Tracking left",
                "OXRLeftHand",
                "OpenXRLeftHand");
        }

        if (rightHand == null)
        {
            rightHand = FindFirstTransformByName(
                "RightHandAnchor",
                "RightControllerAnchor",
                "[BuildingBlock] Controller Tracking Right",
                "[BuildingBlock] Hand Tracking right",
                "OXRRightHand",
                "OpenXRRightHand");
        }

        if (head == null || leftHand == null || rightHand == null)
        {
            Debug.LogWarning(
                $"[{nameof(VrGhostSpawnerFusion)}] Tracking source missing. Head={head}, Left={leftHand}, Right={rightHand}. Drag them manually in the Inspector if the ghost does not move correctly.");
        }
    }

    private static Transform FindFirstTransformByName(params string[] names)
    {
        foreach (string targetName in names)
        {
            GameObject found = GameObject.Find(targetName);
            if (found != null)
            {
                return found.transform;
            }
        }

        return null;
    }
}
