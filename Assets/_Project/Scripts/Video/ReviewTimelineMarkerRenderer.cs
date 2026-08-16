using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public class ReviewTimelineMarkerRenderer : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    [Header("References")]
    [SerializeField] private VideoPlayerController videoPlayerController;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RectTransform markerContainer;
    [SerializeField] private Button markerPrefab;
    [SerializeField] private float markerVerticalOffset = 20f;

    [Header("Action Detail")]
    [SerializeField] private GameObject actionDetailCanvas;
    [SerializeField] private TMP_Text headlineLabel;
    [SerializeField] private TMP_Text contentLabel;

    [Header("Mock/Test")]
    [SerializeField] private TextAsset mockJsonLog;
    [SerializeField] private bool loadMockOnStart;
    [SerializeField] private float mockVideoDuration = 10f;
    [SerializeField] private bool useMockDurationWhenMockLoaded = true;

    [Header("Marker Colors")]
    [SerializeField] private Color correctColor = new Color(0.1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color wrongColor = new Color(0.95f, 0.15f, 0.15f, 1f);

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
            loadedFromMock = false;
            loadedSession = null;
            ClearMarkers();
            Debug.LogWarning($"[{DebugPrefix}] Cannot load review marker JSON: {jsonPath}");
            return;
        }

        loadedFromMock = false;
        LoadFromJsonText(File.ReadAllText(jsonPath));
    }

    public void LoadFromJsonText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot load review markers because JSON text is empty.");
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
            Debug.LogWarning($"[{DebugPrefix}] Cannot render review markers because marker container or prefab is missing.");
            return;
        }

        float duration = GetVideoDurationForMarkers();
        if (duration <= 0f)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        Debug.Log($"[{DebugPrefix}] Rendering {loadedSession.actions.Count} review markers. Duration={duration:0.00}s, ContainerWidth={markerContainer.rect.width:0.00}");

        foreach (PlayerActionLogEntry action in loadedSession.actions)
        {
            CreateMarker(action, duration);
        }
    }

    private void CreateMarker(PlayerActionLogEntry action, float duration)
    {
        Button marker = Instantiate(markerPrefab, markerContainer);
        marker.gameObject.SetActive(true);
        marker.transform.SetAsLastSibling();
        marker.interactable = true;
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
        markerRect.anchoredPosition = new Vector2(markerX, markerVerticalOffset);

        Debug.Log($"[{DebugPrefix}] Review marker '{action.title}' at {action.time:0.00}s => {normalizedTime:P0}, X={markerX:0.00}");

        Image image = marker.GetComponent<Image>();
        if (image != null)
        {
            image.color = GetMarkerColor(action.result);
            image.raycastTarget = true;
        }

        PlayerActionLogEntry markerAction = action;
        EventTrigger eventTrigger = marker.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = marker.gameObject.AddComponent<EventTrigger>();
        }

        if (eventTrigger.triggers == null)
        {
            eventTrigger.triggers = new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry pointerDown = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        pointerDown.callback.AddListener(_ => OnMarkerClicked(markerAction));
        eventTrigger.triggers.Add(pointerDown);

        // Quest controller rays can leave a small marker before PointerUp,
        // so PointerDown is the reliable activation event for timeline markers.
        marker.onClick.RemoveAllListeners();
    }

    private void OnMarkerClicked(PlayerActionLogEntry action)
    {
        Debug.Log($"[{DebugPrefix}] Review marker clicked '{action.title}' at {action.time:0.00}s");

        videoPlayerController?.SeekToTime(action.time);
        ShowActionDetail(action);
    }

    private void ShowActionDetail(PlayerActionLogEntry action)
    {
        if (actionDetailCanvas != null)
        {
            actionDetailCanvas.SetActive(true);
        }

        if (headlineLabel != null)
        {
            headlineLabel.text = action.title;
        }

        if (contentLabel != null)
        {
            contentLabel.text = string.IsNullOrWhiteSpace(action.description)
                ? action.result.ToString()
                : action.description;
        }

        Debug.Log(
            $"[{DebugPrefix}] Review action detail updated: " +
            $"Title='{action.title}', Result='{action.result}', " +
            $"CanvasActive={actionDetailCanvas != null && actionDetailCanvas.activeInHierarchy}");
    }

    private Color GetMarkerColor(PlayerActionResult result)
    {
        switch (result)
        {
            case PlayerActionResult.Correct:
                return correctColor;
            case PlayerActionResult.Incorrect:
                return wrongColor;
            default:
                return wrongColor;
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
