using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HeadCanvasController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string endGameSceneName = "EndGameScene";

    [Header("Head UI")]
    [SerializeField] private GameObject gasHub;
    [SerializeField] private GameObject timeHub;
    [SerializeField] private GameObject gameOverCanvas;
    [SerializeField] private GameObject logCanvas;

    [Header("Time Hub")]
    [SerializeField] private TMP_Text timerText;

    [Header("Log Content (Optional)")]
    [SerializeField] private TMP_Text logTitle;
    [SerializeField] private TMP_Text logContent;

    private bool isMainSceneLoaded;
    private GameFlowManager gameFlowManager;
    private int lastDisplayedSeconds = -1;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplySceneState(SceneManager.GetActiveScene(), hideLog: true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneState(scene, hideLog: true);
    }

    private void Update()
    {
        UpdateTimeHub();
    }

    public void RefreshFromSettings()
    {
        ApplySceneState(SceneManager.GetActiveScene(), hideLog: false);
    }

    public void ShowLog(string title, string content)
    {
        if (SceneManager.GetActiveScene().name != endGameSceneName)
            return;

        if (logTitle != null)
            logTitle.text = title;

        if (logContent != null)
            logContent.text = content;

        SetActiveIfNeeded(logCanvas, true);
    }

    public void HideLog()
    {
        SetActiveIfNeeded(logCanvas, false);
    }

    private void ApplySceneState(Scene scene, bool hideLog)
    {
        isMainSceneLoaded = scene.name == mainSceneName;
        bool isEndGameScene = scene.name == endGameSceneName;

        SetActiveIfNeeded(gameOverCanvas, isEndGameScene);
        SetActiveIfNeeded(gasHub, isMainSceneLoaded && GameSettings.ShowGasLevel);
        SetActiveIfNeeded(timeHub, isMainSceneLoaded && GameSettings.ShowTime);

        gameFlowManager = isMainSceneLoaded ? GameFlowManager.Instance : null;
        lastDisplayedSeconds = -1;

        if (hideLog)
            HideLog();
    }

    private void UpdateTimeHub()
    {
        if (!isMainSceneLoaded ||
            timeHub == null ||
            !timeHub.activeInHierarchy ||
            timerText == null)
        {
            return;
        }

        if (gameFlowManager == null)
            gameFlowManager = GameFlowManager.Instance;

        if (gameFlowManager == null)
            return;

        int total = Mathf.CeilToInt(gameFlowManager.RemainingSeconds);
        if (total == lastDisplayedSeconds)
            return;

        lastDisplayedSeconds = total;
        int minutes = total / 60;
        int seconds = total % 60;
        timerText.text = $"TIME: {minutes:00}:{seconds:00}";
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
