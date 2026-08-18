using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class TutorialStationContent
{
    public string stationId;
    public TutorialStationPanelUI panel;
    public string title;
    [TextArea(2, 5)] public string content;
}

public sealed class TutorialSceneController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string startSceneName = "StartScene";

    [Header("Station text")]
    [Tooltip("Station positions and trigger sizes are edited directly in the TutorialScene hierarchy.")]
    [SerializeField] private List<TutorialStationContent> stations = new();

    private bool returningToStartScene;

    private void Start()
    {
        ApplyStationText();
    }

    [ContextMenu("Apply Station Text")]
    public void ApplyStationText()
    {
        foreach (TutorialStationContent station in stations)
        {
            if (station?.panel != null)
            {
                station.panel.SetContent(station.title, station.content);
            }
        }
    }

    public async void ReturnToStartScene()
    {
        if (returningToStartScene)
        {
            return;
        }

        returningToStartScene = true;

        NetworkRunner runner = GetActiveSinglePlayerRunner();
        if (runner != null)
        {
            try
            {
                await runner.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[{nameof(TutorialSceneController)}] Could not shut down the local runner cleanly: " +
                    exception.Message,
                    this);
            }

            if (runner != null)
            {
                Destroy(runner.gameObject);
            }
        }

        SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
    }

    private static NetworkRunner GetActiveSinglePlayerRunner()
    {
        for (int i = NetworkRunner.Instances.Count - 1; i >= 0; i--)
        {
            NetworkRunner runner = NetworkRunner.Instances[i];
            if (runner != null && runner.IsRunning && runner.IsSinglePlayer)
            {
                return runner;
            }
        }

        return null;
    }
}
