using Fusion;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class HouseEscapeZone : MonoBehaviour
{
    [SerializeField] private BoxCollider zoneCollider;
    [SerializeField] private Transform singlePlayerPositionSource;

    [Header("Runtime Debug")]
    [SerializeField] private int playersInsideCount;
    [SerializeField] private int activePlayerCount;

    private void Awake()
    {
        ResolveCollider();
    }

    private void OnValidate()
    {
        ResolveCollider();
    }

    public bool HaveAllActivePlayersEscaped(NetworkRunner runner)
    {
        if (zoneCollider == null)
            return false;

        if (runner == null || !runner.IsRunning)
            return HasSinglePlayerEscaped();

        int playersInside = 0;
        int activePlayers = 0;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            activePlayers++;

            NetworkObject playerObject = runner.GetPlayerObject(player);
            if (playerObject != null && IsInside(playerObject.transform.position))
                playersInside++;
        }

        activePlayerCount = activePlayers;
        playersInsideCount = playersInside;

        return activePlayerCount > 0 && playersInsideCount == activePlayerCount;
    }

    public void ResetProgress()
    {
        playersInsideCount = 0;
        activePlayerCount = 0;
    }

    private bool HasSinglePlayerEscaped()
    {
        Transform positionSource = singlePlayerPositionSource;
        if (positionSource == null && Camera.main != null)
            positionSource = Camera.main.transform;

        activePlayerCount = positionSource != null ? 1 : 0;
        playersInsideCount = positionSource != null && IsInside(positionSource.position) ? 1 : 0;
        return activePlayerCount > 0 && playersInsideCount == activePlayerCount;
    }

    private bool IsInside(Vector3 worldPosition)
    {
        Vector3 localPosition = zoneCollider.transform.InverseTransformPoint(worldPosition);
        Vector3 offset = localPosition - zoneCollider.center;
        Vector3 halfSize = zoneCollider.size * 0.5f;

        return Mathf.Abs(offset.x) <= halfSize.x &&
               Mathf.Abs(offset.y) <= halfSize.y &&
               Mathf.Abs(offset.z) <= halfSize.z;
    }

    private void ResolveCollider()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<BoxCollider>();

        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        ResolveCollider();
        if (zoneCollider == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;

        Gizmos.matrix = zoneCollider.transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.9f, 0.35f, 0.2f);
        Gizmos.DrawCube(zoneCollider.center, zoneCollider.size);
        Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(zoneCollider.center, zoneCollider.size);

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}
