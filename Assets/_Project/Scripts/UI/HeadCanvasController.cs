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

    [Header("Log Content (Optional)")]
    [SerializeField] private TMP_Text logTitle;
    [SerializeField] private TMP_Text logContent;

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
        bool isMainScene = scene.name == mainSceneName;
        bool isEndGameScene = scene.name == endGameSceneName;

        SetActiveIfNeeded(gameOverCanvas, isEndGameScene);
        SetActiveIfNeeded(gasHub, isMainScene && GameSettings.ShowGasLevel);
        SetActiveIfNeeded(timeHub, isMainScene && GameSettings.ShowTime);

        if (hideLog)
            HideLog();
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
