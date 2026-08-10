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
    private const string ExplosionProofFlashlightId = "ExplosionProofFlashlight";

    [Header("Debug")]
    [SerializeField] private bool logIgnoredEvents;

    private readonly Dictionary<string, HashSet<string>> heldItemsByActor =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, HashSet<string>> activeItemsByActor =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, GasZoneState> gasZoneByActor =
        new Dictionary<string, GasZoneState>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> activeConditionLatches =
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
        heldItemsByActor.Clear();
        activeItemsByActor.Clear();
        gasZoneByActor.Clear();
        activeConditionLatches.Clear();
        evaluatedOneShotActions.Clear();
    }

    private void HandleGameplayEvent(GameplayEvent gameplayEvent)
    {
        string actorId = NormalizeActorId(gameplayEvent.ActorId);

        switch (gameplayEvent.Type)
        {
            case GameplayEventType.HeldItemGrabbed:
                SetItemState(heldItemsByActor, actorId, gameplayEvent.TargetId, true);
                TryEvaluateHeldItemInGas(actorId, gameplayEvent.TargetId, gameplayEvent);
                return;

            case GameplayEventType.HeldItemReleased:
                SetItemState(heldItemsByActor, actorId, gameplayEvent.TargetId, false);
                ClearConditionLatch(actorId, gameplayEvent.TargetId);
                return;

            case GameplayEventType.HeldItemActivated:
                SetItemState(activeItemsByActor, actorId, gameplayEvent.TargetId, true);
                TryEvaluateHeldItemInGas(actorId, gameplayEvent.TargetId, gameplayEvent);
                return;

            case GameplayEventType.HeldItemDeactivated:
                SetItemState(activeItemsByActor, actorId, gameplayEvent.TargetId, false);
                ClearConditionLatch(actorId, gameplayEvent.TargetId);
                return;

            case GameplayEventType.PlayerEnteredGasZone:
                SetGasZoneState(actorId, true, ResolveGasLevel(gameplayEvent));
                EvaluateAllHeldItems(actorId, gameplayEvent);
                return;

            case GameplayEventType.PlayerExitedGasZone:
                SetGasZoneState(actorId, false, ResolveGasLevel(gameplayEvent));
                ClearActorConditionLatches(actorId);
                return;

            case GameplayEventType.GasLevelChanged:
                UpdateActiveGasLevels(ResolveGasLevel(gameplayEvent));
                return;
        }

        EvaluateDirectAction(gameplayEvent, actorId);
    }

    private void EvaluateDirectAction(GameplayEvent gameplayEvent, string actorId)
    {
        int gasLevel = ResolveGasLevel(gameplayEvent);

        switch (gameplayEvent.Type)
        {
            case GameplayEventType.PlayerEscapedHouse:
                PublishOneShot(
                    gameplayEvent, actorId, "ExitHouse", "Thoát ra ngoài",
                    "Bạn đã rời khỏi khu vực xảy ra sự cố.",
                    PlayerActionResult.Correct, 15, gasLevel);
                break;

            case GameplayEventType.ValveClosed:
                PublishOneShot(
                    gameplayEvent, actorId, "CloseMainGasValve", "Khóa bình gas",
                    "Khóa bình gas giúp ngăn khí gas tiếp tục rò rỉ.",
                    PlayerActionResult.Correct, 20, gasLevel);
                break;

            case GameplayEventType.WindowOpened:
                PublishOneShot(
                    gameplayEvent, actorId, "OpenWindow", "Mở cửa sổ",
                    "Mở cửa sổ giúp khí gas thoát ra ngoài.",
                    PlayerActionResult.Correct, 10, gasLevel);
                break;

            case GameplayEventType.DoorOpened:
                PublishOneShot(
                    gameplayEvent, actorId, "OpenFrontDoor", "Mở cửa trước",
                    "Mở cửa trước giúp thông thoáng không gian và tạo lối thoát.",
                    PlayerActionResult.Correct, 10, gasLevel);
                break;

            case GameplayEventType.GateOpened:
                PublishOneShot(
                    gameplayEvent, actorId, "OpenEntranceGate", "Mở cửa rào",
                    "Mở cửa rào giúp tạo lối di chuyển ra khỏi khu vực sự cố.",
                    PlayerActionResult.Correct, 5, gasLevel);
                break;

            case GameplayEventType.ExtinguisherSafetyPinPulled:
                PublishOneShot(
                    gameplayEvent, actorId, "PullExtinguisherSafetyPin", "Mở chốt bình chữa cháy",
                    "Mở chốt an toàn là bước cần thiết trước khi sử dụng bình chữa cháy.",
                    PlayerActionResult.Correct, 10, gasLevel);
                break;

            case GameplayEventType.FireExtinguished:
                if (IsFireExtinguisherSource(gameplayEvent.Payload))
                {
                    PublishOneShot(
                        gameplayEvent, actorId, "ExtinguishFireWithExtinguisher",
                        "Dập lửa bằng bình chữa cháy",
                        "Bạn đã sử dụng bình chữa cháy để dập tắt đám cháy.",
                        PlayerActionResult.Correct, 20, gasLevel);
                }
                else
                {
                    LogIgnored(gameplayEvent, "fire was not extinguished by the fire extinguisher");
                }
                break;

            case GameplayEventType.LightTurnOnAttempted:
                Publish(
                    gameplayEvent, actorId, "TurnOnLight", "Mở đèn",
                    CreateElectricalFeedback("bật đèn", gasLevel),
                    PlayerActionResult.Incorrect, -10, gasLevel);
                break;

            case GameplayEventType.FanTurnOnAttempted:
                Publish(
                    gameplayEvent, actorId, "TurnOnFan", "Mở quạt",
                    CreateElectricalFeedback("bật quạt", gasLevel),
                    PlayerActionResult.Incorrect, -10, gasLevel);
                break;

            default:
                LogIgnored(gameplayEvent, "event is not an evaluated player action");
                break;
        }
    }

    private void TryEvaluateHeldItemInGas(
        string actorId,
        string itemId,
        GameplayEvent sourceEvent)
    {
        itemId = NormalizeItemId(itemId);
        if (string.IsNullOrEmpty(itemId))
            return;

        if (!gasZoneByActor.TryGetValue(actorId, out GasZoneState gasState) ||
            !gasState.IsInside)
        {
            return;
        }

        if (!ContainsItem(heldItemsByActor, actorId, itemId) ||
            !ContainsItem(activeItemsByActor, actorId, itemId))
        {
            return;
        }

        string latchKey = CreateActorItemKey(actorId, itemId);
        if (!activeConditionLatches.Add(latchKey))
            return;

        int gasLevel = GetCurrentGasLevel(gasState.GasLevel);

        if (IsItem(itemId, PhoneFlashlightId))
        {
            Publish(
                sourceEvent, actorId,
                "EnterGasZoneWithPhoneFlashlight",
                "Dùng điện thoại soi sáng trong vùng gas",
                CreatePortableIgnitionFeedback("điện thoại", gasLevel),
                PlayerActionResult.Incorrect, -15, gasLevel);
            return;
        }

        if (IsItem(itemId, LighterId))
        {
            Publish(
                sourceEvent, actorId,
                "EnterGasZoneWithLitLighter",
                "Dùng bật lửa soi sáng trong vùng gas",
                CreatePortableIgnitionFeedback("bật lửa", gasLevel),
                PlayerActionResult.Incorrect, -20, gasLevel);
            return;
        }

        if (IsItem(itemId, ExplosionProofFlashlightId))
        {
            Publish(
                sourceEvent, actorId,
                "EnterGasZoneWithExplosionProofFlashlight",
                "Dùng đèn pin chống cháy nổ trong vùng gas",
                "Đây là hành động đúng vì đèn pin chống cháy nổ không tạo nguồn đánh lửa gây cháy hoặc nổ khí gas.",
                PlayerActionResult.Correct, 10, gasLevel);
            return;
        }

        activeConditionLatches.Remove(latchKey);
        LogIgnored(sourceEvent, $"unknown held item id '{itemId}'");
    }

    private void EvaluateAllHeldItems(string actorId, GameplayEvent sourceEvent)
    {
        if (!heldItemsByActor.TryGetValue(actorId, out HashSet<string> heldItems))
            return;

        string[] snapshot = new string[heldItems.Count];
        heldItems.CopyTo(snapshot);

        foreach (string itemId in snapshot)
            TryEvaluateHeldItemInGas(actorId, itemId, sourceEvent);
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
            sourceEvent, actorId, actionId, actionName, feedback,
            result, scoreDelta, gasLevel);
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
            return $"Hành động {actionName} đã tạo nguồn đánh lửa và gây nổ khí gas.";

        if (gasLevel == 2)
            return $"Hành động {actionName} đã tạo nguồn đánh lửa và gây cháy khí gas.";

        return $"Ở mức gas 1, hành động {actionName} chưa gây cháy nhưng vẫn không nên thực hiện trong khu vực đang rò rỉ khí gas.";
    }

    private static string CreatePortableIgnitionFeedback(string itemName, int gasLevel)
    {
        if (gasLevel >= 3)
            return $"Sử dụng {itemName} trong vùng gas đã tạo nguồn đánh lửa và gây nổ khí gas.";

        if (gasLevel == 2)
            return $"Sử dụng {itemName} trong vùng gas đã tạo nguồn đánh lửa và gây cháy khí gas.";

        return $"Ở mức gas 1, hành động này chưa gây cháy nhưng vẫn không nên sử dụng {itemName} trong khu vực đang rò rỉ khí gas.";
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
        switch (gameplayEvent.Payload)
        {
            case int value:
                return Mathf.Clamp(value, 0, 3);
            case float value:
                return Mathf.Clamp(Mathf.RoundToInt(value), 0, 3);
            case double value:
                return Mathf.Clamp((int)Math.Round(value), 0, 3);
            case long value:
                return Mathf.Clamp((int)value, 0, 3);
        }

        return GetCurrentGasLevel(0);
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

    private static void SetItemState(
        Dictionary<string, HashSet<string>> states,
        string actorId,
        string itemId,
        bool active)
    {
        itemId = NormalizeItemId(itemId);
        if (string.IsNullOrEmpty(itemId))
            return;

        if (!states.TryGetValue(actorId, out HashSet<string> items))
        {
            items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            states[actorId] = items;
        }

        if (active)
            items.Add(itemId);
        else
            items.Remove(itemId);
    }

    private static bool ContainsItem(
        Dictionary<string, HashSet<string>> states,
        string actorId,
        string itemId)
    {
        return states.TryGetValue(actorId, out HashSet<string> items) &&
               items.Contains(itemId);
    }

    private void ClearConditionLatch(string actorId, string itemId)
    {
        activeConditionLatches.Remove(
            CreateActorItemKey(actorId, NormalizeItemId(itemId)));
    }

    private void ClearActorConditionLatches(string actorId)
    {
        string prefix = actorId + "|";
        activeConditionLatches.RemoveWhere(key =>
            key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateActorItemKey(string actorId, string itemId)
    {
        return actorId + "|" + itemId;
    }

    private static string NormalizeActorId(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return "LocalPlayer";

        string normalized = actorId.Trim();
        if (normalized.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Player_", StringComparison.OrdinalIgnoreCase))
        {
            return "LocalPlayer";
        }

        return normalized;
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
