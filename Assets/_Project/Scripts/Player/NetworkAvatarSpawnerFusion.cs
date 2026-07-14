using System.Collections;
using Fusion;
using Meta.XR.MultiplayerBlocks.Fusion;
using UnityEngine;

public class NetworkAvatarSpawnerFusion : MonoBehaviour
{
    [Header("Network Avatar Prefab")]
    [NetworkPrefab]
    [SerializeField] private NetworkObject networkAvatarPrefab;

    [Header("Local Meta Tracking")]
    [SerializeField] private SampleInputManager inputManager;
    [SerializeField] private Transform localRigRoot;
    [SerializeField] private bool autoFindMissingSources = true;

    [Header("Spawn")]
    [SerializeField] private Vector3 spawnPositionOffset;
    [SerializeField] private bool setAsFusionPlayerObject = true;
    [SerializeField] private float spawnReadyTimeoutSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private NetworkObject spawnedAvatar;
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
        if (spawnedAvatar != null)
        {
            spawnRoutine = null;
            yield break;
        }

        float waitStartTime = Time.realtimeSinceStartup;
        while (spawnedAvatar == null && !IsRunnerSpawnReady(runner))
        {
            runner = GetActiveRunner();

            if (spawnReadyTimeoutSeconds > 0f &&
                Time.realtimeSinceStartup - waitStartTime > spawnReadyTimeoutSeconds)
            {
                Debug.LogWarning(
                    $"[{nameof(NetworkAvatarSpawnerFusion)}] Timed out waiting for Fusion runner to become spawn-ready. " +
                    GetRunnerStateText(runner),
                    this);
                spawnRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (spawnedAvatar != null)
        {
            spawnRoutine = null;
            yield break;
        }

        if (networkAvatarPrefab == null)
        {
            Debug.LogError($"[{nameof(NetworkAvatarSpawnerFusion)}] Missing Network Avatar Prefab.", this);
            spawnRoutine = null;
            yield break;
        }

        if (autoFindMissingSources)
        {
            FindMissingSources();
        }

        if (inputManager == null)
        {
            Debug.LogError(
                $"[{nameof(NetworkAvatarSpawnerFusion)}] Missing SampleInputManager. " +
                "Add Meta's Avatar SDK Manager prefab to StartScene first.",
                this);
            spawnRoutine = null;
            yield break;
        }

        connectedRoomToken = runner.SessionInfo?.Name ?? string.Empty;
        activePlayerCount = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
        {
            activePlayerCount++;
        }

        Vector3 spawnPosition = localRigRoot != null
            ? localRigRoot.position + spawnPositionOffset
            : spawnPositionOffset;
        Quaternion spawnRotation = localRigRoot != null ? localRigRoot.rotation : Quaternion.identity;

        try
        {
            spawnedAvatar = runner.Spawn(
                networkAvatarPrefab,
                spawnPosition,
                spawnRotation,
                runner.LocalPlayer,
                (_, avatarObject) =>
                {
                    NetworkAvatarFusion avatar = avatarObject.GetComponent<NetworkAvatarFusion>();
                    if (avatar != null)
                    {
                        avatar.SetLocalSources(inputManager, localRigRoot);
                    }
                });
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"[{nameof(NetworkAvatarSpawnerFusion)}] Could not spawn '{networkAvatarPrefab.name}'. " +
                "Check its FusionPrefab label and rebuild the Network Project Config prefab table. " +
                GetRunnerStateText(runner),
                this);
            Debug.LogException(exception, this);
            spawnRoutine = null;
            yield break;
        }

        if (spawnedAvatar == null)
        {
            Debug.LogError(
                $"[{nameof(NetworkAvatarSpawnerFusion)}] runner.Spawn returned null for '{networkAvatarPrefab.name}'.",
                this);
            spawnRoutine = null;
            yield break;
        }

        if (setAsFusionPlayerObject && runner.GetPlayerObject(runner.LocalPlayer) == null)
        {
            runner.SetPlayerObject(runner.LocalPlayer, spawnedAvatar);
        }

        Debug.Log(
            $"[{nameof(NetworkAvatarSpawnerFusion)}] Spawned local avatar. " +
            $"Room={connectedRoomToken}, LocalPlayer={runner.LocalPlayer}, ActivePlayers={activePlayerCount}, " +
            $"HasInput={spawnedAvatar.HasInputAuthority}, HasState={spawnedAvatar.HasStateAuthority}",
            this);

        spawnRoutine = null;
    }

    private void FindMissingSources()
    {
        if (inputManager == null)
        {
            inputManager = FindFirstObjectByType<SampleInputManager>(FindObjectsInactive.Include);
        }

        if (localRigRoot == null)
        {
            OVRCameraRig cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig != null)
            {
                localRigRoot = cameraRig.transform;
            }
            else if (Camera.main != null)
            {
                localRigRoot = Camera.main.transform.root;
            }
        }
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

    private static bool IsRunnerSpawnReady(NetworkRunner runner)
    {
        return runner != null &&
            runner.IsRunning &&
            runner.LocalPlayer != PlayerRef.None &&
            runner.IsSimulationUpdating &&
            runner.CanSpawn;
    }

    private static string GetRunnerStateText(NetworkRunner runner)
    {
        if (runner == null)
        {
            return "Runner=<null>";
        }

        return $"RunnerState: IsRunning={runner.IsRunning}, LocalPlayer={runner.LocalPlayer}, " +
            $"IsSimulationUpdating={runner.IsSimulationUpdating}, CanSpawn={runner.CanSpawn}, " +
            $"Session={runner.SessionInfo?.Name ?? "<none>"}";
    }
}
