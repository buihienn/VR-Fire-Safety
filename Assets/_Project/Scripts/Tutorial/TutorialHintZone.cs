using System.Collections.Generic;
using UnityEngine;

public sealed class TutorialHintZone : MonoBehaviour
{
    private readonly HashSet<Collider> playerColliders = new();

    [SerializeField] private TutorialStationPanelUI stationPanel;

    public void SetPanel(TutorialStationPanelUI panel)
    {
        stationPanel = panel;
    }

    private void Awake()
    {
        stationPanel?.Hide();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPersistentPlayer(other) || !playerColliders.Add(other))
        {
            return;
        }

        if (playerColliders.Count == 1)
        {
            stationPanel?.Show();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0)
        {
            return;
        }

        stationPanel?.Hide();
    }

    private void OnDisable()
    {
        if (playerColliders.Count > 0)
        {
            playerColliders.Clear();
            stationPanel?.Hide();
        }
    }

    private static bool IsPersistentPlayer(Collider other)
    {
        return other != null && other.GetComponentInParent<PersistentLocalPlayer>() != null;
    }
}
