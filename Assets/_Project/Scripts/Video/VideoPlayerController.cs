using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

public class VideoPlayerController : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    public VideoPlayer videoPlayer;
    public Button playPauseButton;
    public Button skipForwardButton;
    public Button skipBackwardButton;
    public Image playPauseIcon;
    public Sprite playIcon;
    public Sprite pauseIcon;
    public Slider timeSlider;
    public TMP_Text timelineText;
    public GameObject loadingIcon;
    public float skipTime = 10f;
    public bool playLastRecordingOnStart = true;

    private bool isDraggingSlider = false;
    private bool hasInteracted = false;
    private bool hasPendingSeek;
    private float pendingSeekTime;

    public delegate void VideoPlayerInteractionEvent();
    public static event VideoPlayerInteractionEvent OnVideoPlayerInteracted;

    void Start()
    {
        ShowLoadingIcon(false);

        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();

            if (videoPlayer == null)
            {
                Debug.LogError($"[{DebugPrefix}] VideoPlayer component is not assigned or found!");
                return;
            }
        }

        playPauseButton?.onClick.AddListener(() =>
        {
            PlayPause();
            NotifyInteraction();
        });

        skipForwardButton?.onClick.AddListener(() =>
        {
            SkipForward();
            NotifyInteraction();
        });

        skipBackwardButton?.onClick.AddListener(() =>
        {
            SkipBackward();
            NotifyInteraction();
        });

        if (timeSlider != null)
        {
            AddSliderEventHandlers();
            timeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);
        }

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.started += OnVideoStarted;
        videoPlayer.seekCompleted += OnSeekCompleted;

        videoPlayer.playOnAwake = false;
        videoPlayer.Stop();

        ShowLoadingIcon(false);
        UpdatePlayPauseIcon();
        UpdateTimelineText(0);

        if (playLastRecordingOnStart)
        {
            QuestScreenRecordingManager.Instance?.TryPlayLastRecording(this);
        }
    }

    void Update()
    {
        if (videoPlayer == null) return;

        if (!isDraggingSlider && videoPlayer.isPrepared && timeSlider != null)
        {
            timeSlider.value = (float)videoPlayer.time;

            double duration = GetVideoDuration();
            if (duration > 0 && videoPlayer.time >= duration)
            {
                timeSlider.value = timeSlider.maxValue;
            }
        }

        UpdateTimelineText(isDraggingSlider && timeSlider != null ? timeSlider.value : videoPlayer.time);
        UpdatePlayPauseIcon();
    }

    public void PlayVideo(VideoClip clip)
    {
        if (clip == null)
        {
            Debug.LogError($"[{DebugPrefix}] VideoClip is null!");
            return;
        }

        videoPlayer.Stop();
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.Prepare();

        ShowLoadingIcon(true);
        UpdatePlayPauseIcon();
        UpdateTimelineText(0);
    }

    public void PlayVideoUrl(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            Debug.LogError($"[{DebugPrefix}] Video path is null or empty!");
            return;
        }

        string videoUrl = videoPath;
        if (!videoUrl.StartsWith("file://"))
        {
            videoUrl = "file://" + videoUrl;
        }

        videoPlayer.Stop();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoUrl;
        videoPlayer.time = 0;
        videoPlayer.Prepare();
        Debug.Log($"[{DebugPrefix}] Review video URL prepared: {videoUrl}");

        ReviewTimelineMarkerRenderer markerRenderer = GetComponentInChildren<ReviewTimelineMarkerRenderer>(true);
        if (markerRenderer != null)
        {
            markerRenderer.LoadJsonNextToVideo(videoPath);
        }

        ShowLoadingIcon(true);
        UpdatePlayPauseIcon();
        UpdateTimelineText(0);
    }

    void PlayPause()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
        else
        {
            videoPlayer.Play();
        }

        UpdatePlayPauseIcon();
    }

    void SkipForward()
    {
        if (videoPlayer.isPrepared)
        {
            double duration = GetVideoDuration();
            if (duration <= 0) return;

            videoPlayer.time = Mathf.Min(
                (float)videoPlayer.time + skipTime,
                (float)duration
            );
        }
    }

    void SkipBackward()
    {
        if (videoPlayer.isPrepared)
        {
            videoPlayer.time = Mathf.Max(
                (float)videoPlayer.time - skipTime,
                0f
            );
        }
    }

    void AddSliderEventHandlers()
    {
        EventTrigger trigger = timeSlider.gameObject.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger = timeSlider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry pointerDown = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };

        pointerDown.callback.AddListener((eventData) =>
        {
            isDraggingSlider = true;
        });

        trigger.triggers.Add(pointerDown);

        EventTrigger.Entry pointerUp = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };

        pointerUp.callback.AddListener((eventData) =>
        {
            isDraggingSlider = false;

            if (videoPlayer.isPrepared)
            {
                videoPlayer.time = timeSlider.value;
                videoPlayer.Play();
            }

            NotifyInteraction();
        });

        trigger.triggers.Add(pointerUp);
    }

    void OnTimeSliderValueChanged(float value)
    {
        if (isDraggingSlider)
        {
            UpdateTimelineText(value);
        }
    }

    void OnVideoPrepared(VideoPlayer source)
    {
        ShowLoadingIcon(false);

        if (timeSlider != null)
        {
            timeSlider.maxValue = (float)GetVideoDuration();
            timeSlider.value = 0f;
        }

        if (hasPendingSeek)
        {
            float seekTime = pendingSeekTime;
            hasPendingSeek = false;
            SeekToTime(seekTime);
        }

        UpdatePlayPauseIcon();
        UpdateTimelineText(0);
    }

    void OnVideoStarted(VideoPlayer source)
    {
        ShowLoadingIcon(false);
        UpdatePlayPauseIcon();
    }

    void OnSeekCompleted(VideoPlayer source)
    {
        ShowLoadingIcon(false);
    }

    void ShowLoadingIcon(bool show)
    {
        if (loadingIcon != null)
        {
            loadingIcon.SetActive(show);
        }
    }

    void UpdatePlayPauseIcon()
    {
        if (playPauseIcon == null) return;

        playPauseIcon.sprite = videoPlayer != null && videoPlayer.isPlaying
            ? pauseIcon
            : playIcon;
    }

    void UpdateTimelineText(double time)
    {
        if (timelineText == null) return;

        int totalSeconds = Mathf.FloorToInt((float)time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timelineText.text = $"{minutes:00}:{seconds:00}";
    }

    public void SetVideoClip(VideoClip clip)
    {
        if (videoPlayer == null)
        {
            Debug.LogError($"[{DebugPrefix}] VideoPlayer is not assigned.");
            return;
        }

        videoPlayer.clip = clip;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.Prepare();

        ShowLoadingIcon(true);
        UpdatePlayPauseIcon();
        UpdateTimelineText(0);
    }

    public void SeekToTime(float time)
    {
        if (videoPlayer == null || !videoPlayer.isPrepared)
        {
            hasPendingSeek = true;
            pendingSeekTime = Mathf.Max(0f, time);
            Debug.Log($"[{DebugPrefix}] Review video seek queued until prepared: {pendingSeekTime:0.00}s");
            return;
        }

        double duration = GetVideoDuration();
        if (duration > 0)
        {
            time = Mathf.Clamp(time, 0f, (float)duration);
        }
        else
        {
            time = Mathf.Max(0f, time);
        }

        videoPlayer.time = time;
        if (timeSlider != null)
        {
            timeSlider.value = time;
        }

        UpdateTimelineText(time);

        if (!videoPlayer.isPlaying)
        {
            videoPlayer.Play();
        }

        Debug.Log($"[{DebugPrefix}] Review video seek requested: {time:0.00}s");
        NotifyInteraction();
    }

    public double GetDuration()
    {
        return GetVideoDuration();
    }

    private double GetVideoDuration()
    {
        if (videoPlayer == null)
        {
            return 0;
        }

        if (videoPlayer.length > 0)
        {
            return videoPlayer.length;
        }

        if (videoPlayer.clip != null)
        {
            return videoPlayer.clip.length;
        }

        if (videoPlayer.frameCount > 0 && videoPlayer.frameRate > 0)
        {
            return videoPlayer.frameCount / videoPlayer.frameRate;
        }

        return 0;
    }

    void OnDestroy()
    {
        if (videoPlayer == null) return;

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.started -= OnVideoStarted;
        videoPlayer.seekCompleted -= OnSeekCompleted;
    }

    private void NotifyInteraction()
    {
        if (hasInteracted) return;

        hasInteracted = true;
        OnVideoPlayerInteracted?.Invoke();
    }
}
