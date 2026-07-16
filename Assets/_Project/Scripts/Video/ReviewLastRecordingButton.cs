using UnityEngine;

public class ReviewLastRecordingButton : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    [SerializeField] private StartMenuUIManager startMenuUIManager;
    [SerializeField] private VideoPlayerController videoPlayerController;

    public void ShowReviewWithLastRecording()
    {
        if (startMenuUIManager == null)
        {
            startMenuUIManager = FindFirstObjectByType<StartMenuUIManager>();
        }

        if (videoPlayerController == null)
        {
            videoPlayerController = FindFirstObjectByType<VideoPlayerController>(FindObjectsInactive.Include);
        }

        if (startMenuUIManager == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot show review because StartMenuUIManager was not found.");
            return;
        }

        startMenuUIManager.ShowReview();

        if (videoPlayerController == null)
        {
            videoPlayerController = FindFirstObjectByType<VideoPlayerController>(FindObjectsInactive.Include);
        }

        if (videoPlayerController == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot play review video because VideoPlayerController was not found.");
            return;
        }

        QuestScreenRecordingManager manager = QuestScreenRecordingManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot play review video because QuestScreenRecordingManager was not found.");
            return;
        }

        Debug.Log($"[{DebugPrefix}] Review Gameplay clicked. Requesting last recording playback.");
        manager.StopRecordingAndPlayWhenReady(videoPlayerController);
    }
}
