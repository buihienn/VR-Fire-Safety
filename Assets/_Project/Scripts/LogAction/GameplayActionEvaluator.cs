using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EvaluatedGameplayAction
{
    public string sourceEventType;
    public string actionId;
    public string title;
    public string feedback;
    public PlayerActionResult result;
    public string actorId;
    public string targetId;
    public int gasLevel;
    public int scoreDelta;
    public float eventTime;
}

public static class GameplayScoreRuleValues
{
    public const int CloseGasValve = 100;
    public const int LeaveGasArea = 90;
    public const int OpenVentilation = 80;
    public const int ParticipateInFireExtinguishing = 40;

    public const int OperateLightSwitch = -100;
    public const int OperateFanControl = -90;
    public const int ActivateLighter = -100;
    public const int HoldPhone = -60;
    public const int CloseVentilation = -50;
    public const int FaintInGasArea = -40;

    public const int MaximumPositiveScore =
        CloseGasValve +
        LeaveGasArea +
        OpenVentilation +
        ParticipateInFireExtinguishing;
}

public static class GameplayActionEvaluationBus
{
    private const string DebugPrefix = "Record review debug";

    public static event Action<EvaluatedGameplayAction> OnActionEvaluated;

    public static void Raise(EvaluatedGameplayAction action)
    {
        if (action == null)
            return;

        Debug.Log(
            $"[{DebugPrefix}] [GameplayActionEvaluator] Evaluated {action.actionId} " +
            $"as {action.result} | Actor={action.actorId} | GasLevel={action.gasLevel} | " +
            $"ScoreDelta={action.scoreDelta}");

        OnActionEvaluated?.Invoke(action);
    }
}

[DisallowMultipleComponent]
public class GameplayActionEvaluator : MonoBehaviour
{
    private const string PhoneFlashlightId = "PhoneFlashlight";
    private const string LighterId = "Lighter";

    [Header("Debug")]
    [SerializeField] private bool logIgnoredEvents;

    private readonly Dictionary<string, GasZoneState> gasZoneByActor =
        new Dictionary<string, GasZoneState>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> openedVentTargets =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> extinguisherParticipants =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> evaluatedOneShotActions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void OnEnable()
    {
        GameplayEventBus.OnEvent += HandleGameplayEvent;
    }

    private void OnDisable()
    {
        GameplayEventBus.OnEvent -= HandleGameplayEvent;
    }

    public void ResetEvaluationState()
    {
        gasZoneByActor.Clear();
        openedVentTargets.Clear();
        extinguisherParticipants.Clear();
        evaluatedOneShotActions.Clear();
    }

    private void HandleGameplayEvent(GameplayEvent gameplayEvent)
    {
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.IsMatchEnded &&
            gameplayEvent.Type != GameplayEventType.PlayerEscapedHouse)
            return;

        string actorId = NormalizeActorId(gameplayEvent.ActorId);

        switch (gameplayEvent.Type)
        {
            case GameplayEventType.HeldItemGrabbed:
                EvaluatePhoneGrab(gameplayEvent, actorId);
                return;

            case GameplayEventType.HeldItemActivated:
                EvaluateLighterActivation(gameplayEvent, actorId);
                return;

            case GameplayEventType.PlayerEnteredGasZone:
                SetGasZoneState(actorId, true, ResolveGasLevel(gameplayEvent));
                return;

            case GameplayEventType.PlayerExitedGasZone:
                SetGasZoneState(actorId, false, ResolveGasLevel(gameplayEvent));
                return;

            case GameplayEventType.PlayerMovedOutOfGasZone:
                SetGasZoneState(actorId, false, ResolveGasLevel(gameplayEvent));
                return;

            case GameplayEventType.GasLevelChanged:
                UpdateActiveGasLevels(ResolveGasLevel(gameplayEvent));
                return;

            case GameplayEventType.FireExtinguisherApplied:
                RegisterExtinguisherParticipant(actorId);
                return;
        }

        EvaluateDirectAction(gameplayEvent, actorId);
    }

    private void EvaluateDirectAction(GameplayEvent gameplayEvent, string actorId)
    {
        int gasLevel = ResolveGasLevel(gameplayEvent);

        switch (gameplayEvent.Type)
        {
            case GameplayEventType.ValveClosed:
                PublishOneShot(
                    gameplayEvent, actorId,
                    "CloseMainGasValve",
                    "Khóa van bình gas",
                    "Người chơi đã đưa van bình gas về trạng thái đóng, ngăn khí gas tiếp tục thoát ra.",
                    PlayerActionResult.Correct,
                    GameplayScoreRuleValues.CloseGasValve,
                    gasLevel);
                break;

            case GameplayEventType.PlayerEscapedHouse:
                PublishOneShot(
                    gameplayEvent, actorId,
                    "LeaveGasArea",
                    "Thoát khỏi khu vực khí gas",
                    "Người chơi đã đi vào vùng thoát hiểm và hoàn thành màn chơi với kết quả chiến thắng.",
                    PlayerActionResult.Correct,
                    GameplayScoreRuleValues.LeaveGasArea,
                    gasLevel);
                break;

            case GameplayEventType.WindowOpened:
            case GameplayEventType.DoorOpened:
                RecordVentOpened(gameplayEvent);

                if (IsGasPresent(gameplayEvent))
                {
                    PublishOneShot(
                        gameplayEvent, actorId,
                        "OpenVentilation",
                        "Mở cửa để thông gió",
                        "Người chơi đã mở cửa chính hoặc cửa sổ khi trong phòng còn khí gas.",
                        PlayerActionResult.Correct,
                        GameplayScoreRuleValues.OpenVentilation,
                        gasLevel);
                }
                else
                {
                    LogIgnored(gameplayEvent, "ventilation was opened when no gas was present");
                }
                break;

            case GameplayEventType.WindowClosed:
            case GameplayEventType.DoorClosed:
                EvaluateVentClosed(gameplayEvent, actorId, gasLevel);
                break;

            case GameplayEventType.FireExtinguished:
                if (IsFireExtinguisherSource(gameplayEvent.Payload))
                    RegisterExtinguisherParticipant(actorId);

                TryPublishFireExtinguishingCompletion(gameplayEvent, gasLevel);
                break;

            case GameplayEventType.LightSwitchOperated:
            case GameplayEventType.LightTurnOnAttempted:
                if (gasLevel >= 1)
                {
                    PublishOneShot(
                        gameplayEvent, actorId,
                        "OperateLightSwitch",
                        "Thao tác công tắc điện",
                        CreateElectricalFeedback("công tắc điện", gasLevel),
                        PlayerActionResult.Incorrect,
                        GameplayScoreRuleValues.OperateLightSwitch,
                        gasLevel);
                }
                else
                {
                    LogIgnored(gameplayEvent, "light switch was operated below gas level 1");
                }
                break;

            case GameplayEventType.FanControlOperated:
            case GameplayEventType.FanTurnOnAttempted:
                if (gasLevel >= 1)
                {
                    PublishOneShot(
                        gameplayEvent, actorId,
                        "OperateFanControl",
                        "Thao tác quạt điện",
                        CreateElectricalFeedback("núm điều khiển quạt", gasLevel),
                        PlayerActionResult.Incorrect,
                        GameplayScoreRuleValues.OperateFanControl,
                        gasLevel);
                }
                else
                {
                    LogIgnored(gameplayEvent, "fan control was operated below gas level 1");
                }
                break;

            case GameplayEventType.PlayerFainted:
                if (IsActorInsideGasZone(actorId) || gasLevel >= 1)
                {
                    PublishOneShot(
                        gameplayEvent, actorId,
                        "FaintInGasArea",
                        "Bất tỉnh trong vùng khí gas",
                        "Người chơi đã bất tỉnh khi vẫn đang ở trong vùng có khí gas.",
                        PlayerActionResult.Incorrect,
                        GameplayScoreRuleValues.FaintInGasArea,
                        gasLevel);
                }
                else
                {
                    LogIgnored(gameplayEvent, "player fainted outside the gas area");
                }
                break;

            default:
                LogIgnored(gameplayEvent, "event is not part of the scoring rubric");
                break;
        }
    }

    private void EvaluatePhoneGrab(GameplayEvent gameplayEvent, string actorId)
    {
        string itemId = NormalizeItemId(gameplayEvent.TargetId);
        if (!IsItem(itemId, PhoneFlashlightId))
            return;

        int gasLevel = ResolveGasLevel(gameplayEvent);
        PublishOneShot(
            gameplayEvent, actorId,
            "HoldPhone",
            "Sử dụng điện thoại",
            "Người chơi đã cầm điện thoại trong phiên huấn luyện.",
            PlayerActionResult.Incorrect,
            GameplayScoreRuleValues.HoldPhone,
            gasLevel);
    }

    private void EvaluateLighterActivation(GameplayEvent gameplayEvent, string actorId)
    {
        string itemId = NormalizeItemId(gameplayEvent.TargetId);
        if (!IsItem(itemId, LighterId))
            return;

        int gasLevel = ResolveGasLevel(gameplayEvent);
        if (gasLevel < 1)
        {
            LogIgnored(gameplayEvent, "lighter was activated below gas level 1");
            return;
        }

        PublishOneShot(
            gameplayEvent, actorId,
            "ActivateLighter",
            "Sử dụng bật lửa",
            CreatePortableIgnitionFeedback("bật lửa", gasLevel),
            PlayerActionResult.Incorrect,
            GameplayScoreRuleValues.ActivateLighter,
            gasLevel);
    }

    private void RecordVentOpened(GameplayEvent gameplayEvent)
    {
        openedVentTargets.Add(CreateVentKey(gameplayEvent));
    }

    private void EvaluateVentClosed(
        GameplayEvent gameplayEvent,
        string actorId,
        int gasLevel)
    {
        bool wasOpened = openedVentTargets.Remove(CreateVentKey(gameplayEvent));
        if (!wasOpened)
        {
            LogIgnored(gameplayEvent, "ventilation had not been observed in the open state");
            return;
        }

        if (!IsIncidentUnresolved(gameplayEvent))
        {
            LogIgnored(gameplayEvent, "gas leak incident was already fully resolved");
            return;
        }

        PublishOneShot(
            gameplayEvent, actorId,
            "CloseVentilation",
            "Đóng lại cửa thông gió",
            "Người chơi đã đóng cửa chính hoặc cửa sổ khi dòng rò hoặc lượng khí trong phòng chưa được xử lý hoàn toàn.",
            PlayerActionResult.Incorrect,
            GameplayScoreRuleValues.CloseVentilation,
            gasLevel);
    }

    private void RegisterExtinguisherParticipant(string actorId)
    {
        if (!string.IsNullOrWhiteSpace(actorId))
            extinguisherParticipants.Add(actorId);
    }

    private void TryPublishFireExtinguishingCompletion(
        GameplayEvent sourceEvent,
        int gasLevel)
    {
        if (extinguisherParticipants.Count == 0 || !AreAllFiresOut())
            return;

        List<string> participants = new List<string>(extinguisherParticipants);
        participants.Sort(StringComparer.OrdinalIgnoreCase);

        PublishOneShot(
            sourceEvent,
            string.Join(", ", participants),
            "ParticipateInFireExtinguishing",
            "Tham gia dập lửa",
            "Bình chữa cháy do người chơi sử dụng đã tác động đến vùng cháy và toàn bộ các vùng cháy sau đó đã được dập.",
            PlayerActionResult.Correct,
            GameplayScoreRuleValues.ParticipateInFireExtinguishing,
            gasLevel);
    }

    private static bool AreAllFiresOut()
    {
        foreach (FlameNode node in FlameNode.All)
        {
            if (node != null && node.IsBurning)
                return false;
        }

        return true;
    }

    private static bool IsGasPresent(GameplayEvent gameplayEvent)
    {
        if (GasSystem.Instance != null)
            return GasSystem.Instance.HasGasInRoom;

        return ResolveGasLevel(gameplayEvent) >= 1;
    }

    private static bool IsIncidentUnresolved(GameplayEvent gameplayEvent)
    {
        if (GasSystem.Instance != null)
        {
            return GasSystem.Instance.LeakActive ||
                   GasSystem.Instance.HasGasInRoom;
        }

        return ResolveGasLevel(gameplayEvent) >= 1;
    }

    private bool IsActorInsideGasZone(string actorId)
    {
        return gasZoneByActor.TryGetValue(actorId, out GasZoneState state) &&
               state.IsInside;
    }

    private void PublishOneShot(
        GameplayEvent sourceEvent,
        string actorId,
        string actionId,
        string actionName,
        string feedback,
        PlayerActionResult result,
        int scoreDelta,
        int gasLevel)
    {
        if (!evaluatedOneShotActions.Add(actionId))
            return;

        Publish(
            sourceEvent,
            actorId,
            actionId,
            actionName,
            feedback,
            result,
            scoreDelta,
            gasLevel);
    }

    private static void Publish(
        GameplayEvent sourceEvent,
        string actorId,
        string actionId,
        string actionName,
        string feedback,
        PlayerActionResult result,
        int scoreDelta,
        int gasLevel)
    {
        string resultLabel = result == PlayerActionResult.Correct
            ? "Hành động đúng"
            : "Hành động sai";

        GameplayActionEvaluationBus.Raise(new EvaluatedGameplayAction
        {
            sourceEventType = sourceEvent.Type.ToString(),
            actionId = actionId,
            title = $"[{resultLabel}] - {actionName}",
            feedback = feedback,
            result = result,
            actorId = actorId,
            targetId = sourceEvent.TargetId,
            gasLevel = Mathf.Clamp(gasLevel, 0, 3),
            scoreDelta = scoreDelta,
            eventTime = sourceEvent.Time
        });
    }

    private static string CreateElectricalFeedback(string actionName, int gasLevel)
    {
        if (gasLevel >= 3)
            return $"Thao tác {actionName} trong môi trường có khí gas có thể tạo nguồn đánh lửa và gây nổ.";

        if (gasLevel == 2)
            return $"Thao tác {actionName} trong môi trường có khí gas có thể tạo nguồn đánh lửa và gây cháy.";

        return $"Không nên thao tác {actionName} khi hệ thống đã ghi nhận khí gas trong phòng.";
    }

    private static string CreatePortableIgnitionFeedback(string itemName, int gasLevel)
    {
        if (gasLevel >= 3)
            return $"Kích hoạt {itemName} trong vùng gas có thể tạo nguồn đánh lửa và gây nổ.";

        if (gasLevel == 2)
            return $"Kích hoạt {itemName} trong vùng gas có thể tạo nguồn đánh lửa và gây cháy.";

        return $"Không nên kích hoạt {itemName} khi hệ thống đã ghi nhận khí gas trong phòng.";
    }

    private static bool IsFireExtinguisherSource(object payload)
    {
        if (payload is FireExtinguishSource source)
            return source == FireExtinguishSource.FireExtinguisher;

        return payload != null &&
               string.Equals(
                   payload.ToString(),
                   FireExtinguishSource.FireExtinguisher.ToString(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveGasLevel(GameplayEvent gameplayEvent)
    {
        if (EventPayloadContainsGasLevel(gameplayEvent.Type) &&
            TryReadGasLevel(gameplayEvent.Payload, out int payloadGasLevel))
        {
            return payloadGasLevel;
        }

        return GetCurrentGasLevel(0);
    }

    private static bool EventPayloadContainsGasLevel(GameplayEventType eventType)
    {
        switch (eventType)
        {
            case GameplayEventType.GasLevelChanged:
            case GameplayEventType.PlayerEnteredGasZone:
            case GameplayEventType.PlayerExitedGasZone:
            case GameplayEventType.PlayerMovedOutOfGasZone:
            case GameplayEventType.PlayerEnteredDangerZone:
            case GameplayEventType.PlayerExitedDangerZone:
            case GameplayEventType.PlayerFainted:
            case GameplayEventType.LightSwitchOperated:
            case GameplayEventType.LightTurnOnAttempted:
            case GameplayEventType.FanControlOperated:
            case GameplayEventType.FanTurnOnAttempted:
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadGasLevel(object payload, out int gasLevel)
    {
        switch (payload)
        {
            case int value:
                gasLevel = Mathf.Clamp(value, 0, 3);
                return true;
            case float value:
                gasLevel = Mathf.Clamp(Mathf.RoundToInt(value), 0, 3);
                return true;
            case double value:
                gasLevel = Mathf.Clamp((int)Math.Round(value), 0, 3);
                return true;
            case long value:
                gasLevel = Mathf.Clamp((int)value, 0, 3);
                return true;
            default:
                gasLevel = 0;
                return false;
        }
    }

    private static int GetCurrentGasLevel(int fallback)
    {
        return GasSystem.Instance != null
            ? Mathf.Clamp(GasSystem.Instance.GasLevel(), 0, 3)
            : Mathf.Clamp(fallback, 0, 3);
    }

    private void SetGasZoneState(string actorId, bool isInside, int gasLevel)
    {
        gasZoneByActor[actorId] = new GasZoneState
        {
            IsInside = isInside,
            GasLevel = gasLevel
        };
    }

    private void UpdateActiveGasLevels(int gasLevel)
    {
        string[] actors = new string[gasZoneByActor.Count];
        gasZoneByActor.Keys.CopyTo(actors, 0);

        foreach (string actorId in actors)
        {
            GasZoneState state = gasZoneByActor[actorId];
            if (!state.IsInside)
                continue;

            state.GasLevel = gasLevel;
            gasZoneByActor[actorId] = state;
        }
    }

    private static string CreateVentKey(GameplayEvent gameplayEvent)
    {
        string ventType =
            gameplayEvent.Type == GameplayEventType.WindowOpened ||
            gameplayEvent.Type == GameplayEventType.WindowClosed
                ? "Window"
                : "Door";

        string targetId = string.IsNullOrWhiteSpace(gameplayEvent.TargetId)
            ? "UnknownVent"
            : gameplayEvent.TargetId.Trim();

        return ventType + "|" + targetId;
    }

    private static string NormalizeActorId(string actorId)
    {
        return string.IsNullOrWhiteSpace(actorId)
            ? "LocalPlayer"
            : actorId.Trim();
    }

    private static string NormalizeItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return string.Empty;

        return itemId.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool IsItem(string actualId, string expectedId)
    {
        return string.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase);
    }

    private void LogIgnored(GameplayEvent gameplayEvent, string reason)
    {
        if (!logIgnoredEvents)
            return;

        Debug.Log(
            $"[Record review debug] [GameplayActionEvaluator] Ignored {gameplayEvent.Type}: {reason}",
            this);
    }

    private struct GasZoneState
    {
        public bool IsInside;
        public int GasLevel;
    }
}
