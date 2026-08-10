using System;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    [Header("Score")]
    [Min(1)]
    [SerializeField] private int maxScore = 100;
    [SerializeField] private int totalScore;
    [SerializeField] private int earnedScore;
    [SerializeField] private int penaltyScore;
    [SerializeField] private int correctActionCount;
    [SerializeField] private int incorrectActionCount;

    [Header("Debug")]
    [SerializeField] private bool logScoreChanges = true;

    public int TotalScore => totalScore;
    public int EarnedScore => earnedScore;
    public int PenaltyScore => penaltyScore;
    public int CorrectActionCount => correctActionCount;
    public int IncorrectActionCount => incorrectActionCount;
    public event Action<int> ScoreChanged;

    public static int LastScore { get; private set; }

    private readonly HashSet<string> awardedCorrectActions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        ResetScore();
    }

    private void OnEnable()
    {
        GameplayActionEvaluationBus.OnActionEvaluated += HandleEvaluatedAction;
    }

    private void OnDisable()
    {
        GameplayActionEvaluationBus.OnActionEvaluated -= HandleEvaluatedAction;
    }

    public void ResetScore()
    {
        earnedScore = 0;
        penaltyScore = 0;
        correctActionCount = 0;
        incorrectActionCount = 0;
        awardedCorrectActions.Clear();

        totalScore = 0;
        LastScore = 0;
        ScoreChanged?.Invoke(totalScore);
    }

    public void AddScore(int value)
    {
        if (value >= 0)
            earnedScore += value;
        else
            penaltyScore += Mathf.Abs(value);

        RecalculateScore();
    }

    private void HandleEvaluatedAction(EvaluatedGameplayAction action)
    {
        if (action == null)
            return;

        if (action.result == PlayerActionResult.Correct)
        {
            if (!awardedCorrectActions.Add(action.actionId))
            {
                if (logScoreChanges)
                {
                    Debug.Log(
                        $"[{DebugPrefix}] [ScoreManager] Correct action {action.actionId} " +
                        "was already awarded; score unchanged.",
                        this);
                }

                return;
            }

            earnedScore += Mathf.Max(0, action.scoreDelta);
            correctActionCount++;
        }
        else
        {
            penaltyScore += Mathf.Abs(action.scoreDelta);
            incorrectActionCount++;
        }

        RecalculateScore();
    }

    private void RecalculateScore()
    {
        SetScore(Mathf.Clamp(earnedScore - penaltyScore, 0, maxScore));
    }

    private void SetScore(int value)
    {
        bool changed = totalScore != value;
        totalScore = value;
        LastScore = totalScore;

        if (changed)
            ScoreChanged?.Invoke(totalScore);

        if (logScoreChanges)
        {
            Debug.Log(
                $"[{DebugPrefix}] [ScoreManager] Score={totalScore}/{maxScore} " +
                $"| Earned={earnedScore} | Penalty={penaltyScore} " +
                $"| Correct={correctActionCount} | Incorrect={incorrectActionCount}",
                this);
        }
    }

}
