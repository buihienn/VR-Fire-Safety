using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;

public class GameFlowManager : NetworkBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    private enum EndReason
    {
        Win = 0,
        TimeUp = 1,
        PlayerFainted = 2,
        GasExplosion = 3
    }

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
    [SerializeField] private float matchDurationSeconds = 300f;
    [SerializeField] private float returnDelaySeconds = 5f;

    [Range(0f, 0.2f)]
    [SerializeField] private float gasSafeThreshold01 = 0.01f;

    [SerializeField] private bool requireLeakStopped = true;

    [Header("Disable Scripts When End")]
    [SerializeField] private UnityEngine.Behaviour[] behavioursToDisableOnEnd;

    [Header("Scene")]
    [SerializeField] private SceneTransitionManager sceneTransitionManager;
    [SerializeField] private int endGameSceneIndex = 2;

    [Header("Debug")]
    [SerializeField] private float remainingSeconds;
    [SerializeField] private bool matchEnded;
    [SerializeField] private bool playerWon;
    [SerializeField] private string endReason;
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool localEndApplied;

    [Networked] private float RemainingSecondsNet { get; set; }
    [Networked] private bool MatchEndedNet { get; set; }
    [Networked] private bool PlayerWonNet { get; set; }
    [Networked] private int EndReasonNet { get; set; }

    private Coroutine returnRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GameFlowManager found. Destroying duplicate.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        ApplySetting();

        if (gasSystem == null)
            gasSystem = FindFirstObjectByType<GasSystem>();

        if (playerGasExposure == null)
            playerGasExposure = FindFirstObjectByType<PlayerGasExposure>();
    }

    private void Start()
    {
        remainingSeconds = matchDurationSeconds;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShot("VO_StartGame");

        if (endPanel != null)
            endPanel.SetActive(false);

        UpdateTimerUI();
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
        {
            RemainingSecondsNet = matchDurationSeconds;
            MatchEndedNet = false;
            PlayerWonNet = false;
            EndReasonNet = -1;
        }

        remainingSeconds = RemainingSecondsNet;
        UpdateTimerUI();
    }

    private void Update()
    {
        if (!fusionSpawned)
        {
            // Single-player fallback: chạy giống code cũ.
            if (matchEnded) return;
            ProcessMatch(Time.deltaTime);
            return;
        }

        // Khi Fusion đã chạy, cả Host và Client chỉ hiển thị snapshot network
        // trong Update. Không dùng Runner.DeltaTime tại đây vì Update chạy theo
        // FPS và có thể trừ cùng một network tick nhiều lần.
        remainingSeconds = RemainingSecondsNet;
        matchEnded = MatchEndedNet;
        playerWon = PlayerWonNet;
        UpdateTimerUI();

        // RPC xử lý trường hợp realtime. Nhánh này bảo đảm Client vào trễ vẫn
        // áp dụng màn hình kết thúc từ trạng thái Networked hiện tại.
        if (matchEnded &&
            !localEndApplied &&
            EndReasonNet >= (int)EndReason.Win &&
            EndReasonNet <= (int)EndReason.GasExplosion)
        {
            ApplyEndLocal((EndReason)EndReasonNet);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (MatchEndedNet)
            return;

        // Networked value là nguồn dữ liệu authority/rollback.
        // Chỉ giảm đúng một lần cho mỗi Fusion simulation tick.
        remainingSeconds = RemainingSecondsNet;
        ProcessMatch(Runner.DeltaTime);
    }

    private void ProcessMatch(float deltaTime)
    {
        if (CheckWinCondition())
        {
            EndAsWin();
            return;
        }

        remainingSeconds -= deltaTime;

        if (remainingSeconds < 0f)
            remainingSeconds = 0f;

        if (fusionSpawned && Object.HasStateAuthority)
            RemainingSecondsNet = remainingSeconds;

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
        ReportPlayerFainted();
    }

    public void ReportPlayerFainted()
    {
        if (!fusionSpawned)
        {
            EndAsPlayerFainted();
            return;
        }

        if (Object.HasStateAuthority)
        {
            EndAsPlayerFainted();
        }
        else
        {
            RPC_RequestPlayerFainted();
        }
    }

    public void ReportGasExplosion()
    {
        if (!fusionSpawned)
        {
            EndAsGasExplosion();
            return;
        }

        if (Object.HasStateAuthority)
        {
            EndAsGasExplosion();
        }
        else
        {
            RPC_RequestGasExplosion();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPlayerFainted(RpcInfo info = default)
    {
        EndAsPlayerFainted();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestGasExplosion(RpcInfo info = default)
    {
        EndAsGasExplosion();
    }

    private void EndAsTimeUp()
    {
        EndMatch(EndReason.TimeUp);
    }

    private void EndAsWin()
    {
        EndMatch(EndReason.Win);
    }

    private void EndAsPlayerFainted()
    {
        EndMatch(EndReason.PlayerFainted);
    }

    private void EndAsGasExplosion()
    {
        EndMatch(EndReason.GasExplosion);
    }

    private void EndMatch(EndReason reason)
    {
        if (!fusionSpawned)
        {
            ApplyEndLocal(reason);
            return;
        }

        if (!Object.HasStateAuthority)
            return;

        if (MatchEndedNet)
            return;

        MatchEndedNet = true;
        PlayerWonNet = reason == EndReason.Win;
        EndReasonNet = (int)reason;

        RPC_ApplyEndMatch((int)reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyEndMatch(int reasonValue)
    {
        EndReason reason = (EndReason)reasonValue;
        ApplyEndLocal(reason);
    }

    private void ApplyEndLocal(EndReason reason)
    {
        if (localEndApplied)
            return;

        localEndApplied = true;
        matchEnded = true;

        GetEndMessage(reason, out bool won, out bool timeUp, out string title, out string body);

        playerWon = won;
        endReason = body;

        GameOverPayload.Set(won, timeUp, title, body);

        if (hubGas != null)
            hubGas.SetActive(false);

        if (endPanel != null)
            endPanel.SetActive(true);

        if (endTitleText != null)
            endTitleText.text = title;

        if (endBodyText != null)
            endBodyText.text = body;

        if (behavioursToDisableOnEnd != null)
        {
            for (int i = 0; i < behavioursToDisableOnEnd.Length; i++)
            {
                if (behavioursToDisableOnEnd[i] != null)
                    behavioursToDisableOnEnd[i].enabled = false;
            }
        }

        PlayEndAudio(reason);

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnToEndGameSceneRoutine());
    }

    private void GetEndMessage(
        EndReason reason,
        out bool won,
        out bool timeUp,
        out string title,
        out string body)
    {
        won = false;
        timeUp = false;
        title = "GAME OVER";
        body = "";

        switch (reason)
        {
            case EndReason.Win:
                won = true;
                timeUp = false;
                title = "YOU WIN";
                body = "Ban da xu ly an toan. Moi truong da het nguy hiem.";
                break;

            case EndReason.TimeUp:
                won = false;
                timeUp = true;
                title = "TIME UP";
                body = "Thoi gian da het. Ban da khong xu li su co kip thoi.";
                break;

            case EndReason.PlayerFainted:
                won = false;
                timeUp = false;
                title = "GAME OVER";
                body = "Ban da o trong khu vuc gas qua lau va bi o nhiem khi gas.";
                break;

            case EndReason.GasExplosion:
                won = false;
                timeUp = false;
                title = "GAS EXPLOSION";
                body = "Nguon lua da kich hoat vu no khi gas trong phong.";
                break;
        }
    }

    private void PlayEndAudio(EndReason reason)
    {
        if (AudioManager.Instance == null)
            return;

        switch (reason)
        {
            case EndReason.Win:
                AudioManager.Instance.PlayOneShot("VO_GameWin");
                break;

            case EndReason.TimeUp:
                AudioManager.Instance.PlayOneShot("VO_TimeUp");
                break;

            case EndReason.PlayerFainted:
            case EndReason.GasExplosion:
                AudioManager.Instance.PlayOneShot("VO_GameOver");
                break;
        }
    }

    private IEnumerator ReturnToEndGameSceneRoutine()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);

        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();

        if (sceneTransitionManager != null)
            sceneTransitionManager.GoToScene(endGameSceneIndex);
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
            EndAsPlayerFainted();
    }
}
