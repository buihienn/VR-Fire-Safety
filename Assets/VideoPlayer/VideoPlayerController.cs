using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

namespace NudleNexus.Classroom
{
    public class VideoPlayerController : MonoBehaviour
    {
        public VideoPlayer videoPlayer;
        public Button playPauseButton;
        public Button skipForwardButton;
        public Button skipBackwardButton;
        public Slider timeSlider;
        public Slider volumeSlider;
        public GameObject loadingIcon;
        public float skipTime = 10f;

        private bool isDraggingSlider = false;
        private bool hasInteracted = false;

        // Event to notify interaction
        public delegate void VideoPlayerInteractionEvent();
        public static event VideoPlayerInteractionEvent OnVideoPlayerInteracted;

        void Start()
        {
            // Ensure loading icon is hidden initially
            ShowLoadingIcon(false);

            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null)
                {
                    Debug.LogError("VideoPlayer component is not assigned or found!");
                    return;
                }
            }

            playPauseButton?.onClick.AddListener(() => { PlayPause(); NotifyInteraction(); });
            skipForwardButton?.onClick.AddListener(() => { SkipForward(); NotifyInteraction(); });
            skipBackwardButton?.onClick.AddListener(() => { SkipBackward(); NotifyInteraction(); });

            if (timeSlider != null)
            {
                AddSliderEventHandlers();
            }

            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.started += OnVideoStarted;
            videoPlayer.seekCompleted += OnSeekCompleted;

            // Ensure the video does not autoplay
            videoPlayer.playOnAwake = false;
            videoPlayer.Stop();

            ShowLoadingIcon(false);
        }

        void Update()
        {
            // Update time slider during playback
            if (!isDraggingSlider && videoPlayer.isPlaying && timeSlider != null)
            {
                timeSlider.value = (float)videoPlayer.time;

                // Ensure slider reaches max when the video ends
                if (videoPlayer.time >= videoPlayer.clip.length)
                {
                    timeSlider.value = timeSlider.maxValue;
                }
            }
        }

        public void PlayVideo(VideoClip clip)
        {
            if (clip == null)
            {
                Debug.LogError("VideoClip is null!");
                return;
            }

            videoPlayer.Stop();
            videoPlayer.clip = clip;
            videoPlayer.Prepare();
            ShowLoadingIcon(true);
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
        }

        void SkipForward()
        {
            if (videoPlayer.isPrepared && videoPlayer.clip != null)
            {
                videoPlayer.time = Mathf.Min((float)videoPlayer.time + skipTime, (float)videoPlayer.clip.length);
            }
        }

        void SkipBackward()
        {
            if (videoPlayer.isPrepared && videoPlayer.clip != null)
            {
                videoPlayer.time = Mathf.Max((float)videoPlayer.time - skipTime, 0);
            }
        }

        void AddSliderEventHandlers()
        {
            EventTrigger trigger = timeSlider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = timeSlider.gameObject.AddComponent<EventTrigger>();

            // Pointer Down
            EventTrigger.Entry pointerDown = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };
            pointerDown.callback.AddListener((eventData) => { isDraggingSlider = true; });
            trigger.triggers.Add(pointerDown);

            // Pointer Up
            EventTrigger.Entry pointerUp = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerUp
            };
            pointerUp.callback.AddListener((eventData) =>
            {
                isDraggingSlider = false;
                if (videoPlayer.isPrepared && videoPlayer.clip != null)
                {
                // Directly set the time to the slider value
                videoPlayer.time = timeSlider.value;
                    videoPlayer.Play();
                }
                NotifyInteraction(); // Notify interaction on slider release
        });
            trigger.triggers.Add(pointerUp);
        }

        void SetVolume(float value)
        {
            videoPlayer.SetDirectAudioVolume(0, value);
        }

        void OnVideoPrepared(VideoPlayer source)
        {
            ShowLoadingIcon(false);
            if (timeSlider != null && videoPlayer.clip != null)
            {
                timeSlider.maxValue = (float)videoPlayer.clip.length;
                timeSlider.value = 0;
            }
        }

        void OnVideoStarted(VideoPlayer source)
        {
            ShowLoadingIcon(false);
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

        public void SetVideoClip(VideoClip clip)
        {
            if (videoPlayer != null)
            {
                videoPlayer.clip = clip;
                videoPlayer.Prepare();
            }
            else
            {
                Debug.LogError("VideoPlayer is not assigned.");
            }
        }

        void OnDestroy()
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.seekCompleted -= OnSeekCompleted;
        }

        private void NotifyInteraction()
        {
            if (!hasInteracted)
            {
                hasInteracted = true; // Ensure the interaction is tracked only once
                OnVideoPlayerInteracted?.Invoke();
            }
        }
    }
}