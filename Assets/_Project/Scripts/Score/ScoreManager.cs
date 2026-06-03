using System;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private int totalScore;
    [SerializeField] private List<ScoreRule> rules = new();
    [SerializeField] private bool logScoreChanges = true;

    public int TotalScore => totalScore;
    public event Action<int> ScoreChanged;

    public static int LastScore { get; private set; }

    private void Start() {
        ResetScore();
    }

    private void OnEnable()
    {
        GameplayEventBus.OnEvent += HandleGameplayEvent;
    }

    private void OnDisable()
    {
        GameplayEventBus.OnEvent -= HandleGameplayEvent;
    }

    private void Reset()
    {
        rules = new List<ScoreRule>
        {
            new ScoreRule { EventType = GameplayEventType.ValveClosed, ScoreDelta = 15 },
            new ScoreRule { EventType = GameplayEventType.WindowOpened, ScoreDelta = 10 },
            new ScoreRule { EventType = GameplayEventType.FireExtinguished, ScoreDelta = 20 },
            new ScoreRule { EventType = GameplayEventType.PlayerEnteredDangerZone, ScoreDelta = -5 },
            new ScoreRule { EventType = GameplayEventType.WrongActionPerformed, ScoreDelta = -10 }
        };
    }

    public void ResetScore()
    {
        SetScore(0);
    }

    public void AddScore(int value)
    {
        SetScore(totalScore + value);
    }

    private void HandleGameplayEvent(GameplayEvent gameplayEvent)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            ScoreRule rule = rules[i];
            if (rule == null) continue;
            if (rule.EventType != gameplayEvent.Type) continue;

            AddScore(rule.ScoreDelta);
            return;
        }
    }

    private void SetScore(int value)
    {
        if (totalScore == value) return;

        totalScore = value;
        LastScore = totalScore;

        ScoreChanged?.Invoke(totalScore);

        if (logScoreChanges)
            Debug.Log($"[ScoreManager] Score changed: {totalScore}", this);
    }
}
