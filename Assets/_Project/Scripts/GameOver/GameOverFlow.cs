using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverFlow : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text messageText;
    public TMP_Text countdownText;

    [Header("Scene Flow")]
    [Min(0.1f)] public float delayBeforeLoad = 4f;
    public string sceneToLoad = "WaitingRoom";

    private bool started;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void ShowGameOverAndLoadScene()
    {
        if (started) return;
        started = true;
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (titleText != null)
            titleText.text = "GAME OVER";

        if (messageText != null)
            messageText.text = "Ban da bat tinh vi hit khi gas.";

        float timeLeft = delayBeforeLoad;

        while (timeLeft > 0f)
        {
            if (countdownText != null)
                countdownText.text = "Quay ve phong cho sau " + Mathf.CeilToInt(timeLeft) + "s";

            timeLeft -= Time.unscaledDeltaTime;
            yield return null;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}