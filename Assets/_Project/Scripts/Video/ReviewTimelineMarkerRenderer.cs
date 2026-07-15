using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class ReviewTimelineMarkerRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VideoPlayerController videoPlayerController;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RectTransform markerContainer;
    [SerializeField] private Button markerPrefab;

    [Header("Mock/Test")]
    [SerializeField] private TextAsset mockJsonLog;
    [SerializeField] private bool loadMockOnStart;
    [SerializeField] private float mockVideoDuration = 10f;
    [SerializeField] private bool useMockDurationWhenMockLoaded = true;

    [Header("Marker Colors")]
    [SerializeField] private Color correctColor = new Color(0.1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color wrongColor = new Color(0.95f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color neutralColor = new Color(1f, 0.8f, 0.15f, 1f);

    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private PlayerActionLogSession loadedSession;
    private bool loadedFromMock;

    private void Awake()
    {
        if (videoPlayerController == null)
        {
            videoPlayerController = GetComponentInParent<VideoPlayerController>();
        }

        if (videoPlayer == null && videoPlayerController != null)
        {
            videoPlayer = videoPlayerController.videoPlayer;
        }
    }

    private void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
        }

        if (loadMockOnStart && mockJsonLog != null)
        {
            loadedFromMock = true;
            LoadFromJsonText(mockJsonLog.text);
        }
    }

    public void LoadFromJsonFile(string jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
        {
            Debug.LogWarning("Cannot load review marker JSON: " + jsonPath);
            return;
        }

        loadedFromMock = false;
        LoadFromJsonText(File.ReadAllText(jsonPath));
    }

    public void LoadFromJsonText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("Cannot load review markers because JSON text is empty.");
            return;
        }

        loadedSession = JsonUtility.FromJson<PlayerActionLogSession>(json);
        RenderMarkers();
    }

    public void LoadJsonNextToVideo(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            return;
        }

        LoadFromJsonFile(Path.ChangeExtension(videoPath, ".json"));
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        RenderMarkers();
    }

    private void RenderMarkers()
    {
        ClearMarkers();

        if (loadedSession == null || loadedSession.actions == null || loadedSession.actions.Count == 0)
        {
            return;
        }

        if (markerContainer == null || markerPrefab == null)
        {
            Debug.LogWarning("Cannot render review markers because marker container or prefab is missing.");
            return;
        }

        float duration = GetVideoDurationForMarkers();
        if (duration <= 0f)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        Debug.Log($"Rendering {loadedSession.actions.Count} review markers. Duration={duration:0.00}s, ContainerWidth={markerContainer.rect.width:0.00}");

        foreach (PlayerActionLogEntry action in loadedSession.actions)
        {
            CreateMarker(action, duration);
        }
    }

    private void CreateMarker(PlayerActionLogEntry action, float duration)
    {
        Button marker = Instantiate(markerPrefab, markerContainer);
        marker.gameObject.SetActive(true);
        spawnedMarkers.Add(marker.gameObject);

        RectTransform markerRect = marker.GetComponent<RectTransform>();
        float normalizedTime = Mathf.Clamp01(action.time / duration);
        float containerWidth = markerContainer.rect.width;
        float minX = -containerWidth * markerContainer.pivot.x;
        float maxX = containerWidth * (1f - markerContainer.pivot.x);
        float markerX = Mathf.Lerp(minX, maxX, normalizedTime);

        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(markerX, 0f);

        Debug.Log($"Review marker '{action.title}' at {action.time:0.00}s => {normalizedTime:P0}, X={markerX:0.00}");

        Image image = marker.GetComponent<Image>();
        if (image != null)
        {
            image.color = GetMarkerColor(action.result);
        }

        marker.onClick.RemoveAllListeners();
        marker.onClick.AddListener(() =>
        {
            videoPlayerController?.SeekToTime(action.time);
        });
    }

    private Color GetMarkerColor(PlayerActionResult result)
    {
        switch (result)
        {
            case PlayerActionResult.Correct:
                return correctColor;
            case PlayerActionResult.Wrong:
                return wrongColor;
            default:
                return neutralColor;
        }
    }

    private float GetVideoDurationForMarkers()
    {
        if (loadedFromMock && useMockDurationWhenMockLoaded && mockVideoDuration > 0f)
        {
            return mockVideoDuration;
        }

        if (videoPlayerController != null)
        {
            double duration = videoPlayerController.GetDuration();
            if (duration > 0)
            {
                return (float)duration;
            }
        }

        return mockVideoDuration;
    }

    private void ClearMarkers()
    {
        foreach (GameObject marker in spawnedMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        spawnedMarkers.Clear();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
    }
}
