using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private PlayerGasExposure playerGasExposure;

    [Header("UI")]
    [SerializeField] private GameObject hubGas;
    [SerializeField] private GameObject timeLabel;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endBodyText;
    [SerializeField] private TMP_Text timerText;

    [Header("Match Rules")]
    [SerializeField] private float matchDurationSeconds = 300f;   // 5 phut
    [SerializeField] private float returnDelaySeconds = 5f;
    [SerializeField] private string returnSceneName = "StartScene";

    [Range(0f, 0.2f)]
    [SerializeField] private float gasSafeThreshold01 = 0.01f;

    [SerializeField] private bool requireLeakStopped = true;

    [Header("Disable Scripts When End")]
    [SerializeField] private Behaviour[] behavioursToDisableOnEnd;

    [Header("Debug")]
    [SerializeField] private float remainingSeconds;
    [SerializeField] private bool matchEnded;
    [SerializeField] private bool playerWon;
    [SerializeField] private string endReason;

    private void Awake()
    {
        ApplySetting();

        if (gasSystem == null)
            gasSystem = FindFirstObjectByType<GasSystem>();

        if (playerGasExposure == null)
            playerGasExposure = FindFirstObjectByType<PlayerGasExposure>();
    }

    private void Start()
    {
        remainingSeconds = matchDurationSeconds;

        AudioManager.Instance.PlayOneShot("VO_StartGame");

        if (endPanel != null)
            endPanel.SetActive(false);

        UpdateTimerUI();
    }

    private void Update()
    {
        if (matchEnded)
            return;

        if (CheckWinCondition())
        {
            EndAsWin();
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds < 0f)
            remainingSeconds = 0f;

        UpdateTimerUI();

        if (remainingSeconds <= 0f)
        {
            if (CheckWinCondition())
                EndAsWin();
            else
                EndAsTimeUp();
        }
    }

    public void ApplySetting()
    {
        if (timeLabel != null)
            timeLabel.SetActive(true);

        if (hubGas != null)
            hubGas.SetActive(GameSettings.ShowGasLevel);

        Debug.Log($"Applied GameSettings: ShowGasLevel={GameSettings.ShowGasLevel}");
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int total = Mathf.CeilToInt(remainingSeconds);
        int minutes = total / 60;
        int seconds = total % 60;

        timerText.text = $"TIME: {minutes:00}:{seconds:00}";
    }

    public void HandlePlayerFainted()
    {
        if (matchEnded) return;

        EndMatch(
            won: false,
            title: "GAME OVER",
            body: "Ban da hit phai khi gas qua lau va bi ngat xiu."
        );
    }

    private void EndAsTimeUp()
    {
        EndMatch(
            won: false,
            title: "GAME OVER",
            body: "Da het thoi gian 5 phut. Ban chua xu ly xong ro ri khi gas."
        );
    }

    private void EndAsWin()
    {
        EndMatch(
            won: true,
            title: "CHIEN THANG",
            body: "Ban da xu ly an toan: moi truong da het nguy hiem, khong con ro gas va khong con lua."
        );
    }

    private void EndMatch(bool won, string title, string body)
    {
        if (matchEnded) return;

        matchEnded = true;
        playerWon = won;
        endReason = body;

        if (hubGas != null)
            hubGas.SetActive(false);

        if (behavioursToDisableOnEnd != null)
        {
            for (int i = 0; i < behavioursToDisableOnEnd.Length; i++)
            {
                if (behavioursToDisableOnEnd[i] != null)
                    behavioursToDisableOnEnd[i].enabled = false;
            }
        }

        if (endPanel != null)
            endPanel.SetActive(true);

        if (endTitleText != null)
            endTitleText.text = title;

        if (endBodyText != null)
            endBodyText.text = body + "\n\nDang quay ve phong cho...";

        StartCoroutine(ReturnToStartSceneRoutine());
    }

    private IEnumerator ReturnToStartSceneRoutine()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);
        SceneManager.LoadScene(returnSceneName);
    }

    private bool CheckWinCondition()
    {
        if (gasSystem == null)
            return false;

        bool gasSafe = IsGasSafe();
        bool leakStopped = IsLeakStopped();
        bool firesResolved = AreAllFiresResolved();

        return gasSafe && leakStopped && firesResolved;
    }

    private bool IsGasSafe()
    {
        if (gasSystem == null) return false;
        return gasSystem.gas01 <= gasSafeThreshold01 || gasSystem.GasLevel() == 0;
    }

    private bool IsLeakStopped()
    {
        if (gasSystem == null) return false;
        if (!requireLeakStopped) return true;
        return !gasSystem.LeakActive;
    }

    private bool AreAllFiresResolved()
    {
        foreach (FlameNode node in FlameNode.All)
        {
            if (node == null) continue;

            if (node.IsBurning)
                return false;

            if (node.Burn01 > 0.02f)
                return false;
        }

        return true;
    }

    [ContextMenu("Force Win")]
    private void ForceWin()
    {
        if (!matchEnded)
            EndAsWin();
    }

    [ContextMenu("Force Time Up Lose")]
    private void ForceTimeUpLose()
    {
        if (!matchEnded)
            EndAsTimeUp();
    }

    [ContextMenu("Force Faint Lose")]
    private void ForceFaintLose()
    {
        if (!matchEnded)
            HandlePlayerFainted();
    }
}