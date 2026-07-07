using Fusion;
using Meta.XR.MultiplayerBlocks.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartSceneLobbyUI : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private int gameSceneBuildIndex = 4;

    [Header("Scene References")]
    [SerializeField] private CustomMatchmaking customMatchmaking;
    [SerializeField] private LobbyNetworkSceneStart lobbySceneStart;

    [Header("UI References")]
    [SerializeField] private TMP_Text roomIdText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_InputField joinRoomInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button startButton;

    private string currentRoomId = string.Empty;
    private bool busy;
    private bool roomCreated;
    private TouchScreenKeyboard roomIdKeyboard;
    private EventTrigger roomIdEventTrigger;
    private EventTrigger.Entry roomIdPointerClickEntry;

    private void Awake()
    {
        ResolveMissingReferences();
        RegisterRoomIdInputKeyboard();
    }

    private void OnDestroy()
    {
        if (joinRoomInput == null)
        {
            return;
        }

        joinRoomInput.onSelect.RemoveListener(OpenRoomIdKeyboard);
        if (roomIdEventTrigger != null && roomIdPointerClickEntry != null)
        {
            roomIdEventTrigger.triggers.Remove(roomIdPointerClickEntry);
        }
    }

    private void Update()
    {
        SyncRoomIdKeyboard();
        UpdateStatus();
    }

    public async void CreateRoom()
    {
        if (!CanUseMatchmaking())
        {
            return;
        }

        try
        {
            SetBusy(true, "Creating room...");
            CustomMatchmaking.RoomOperationResult result = await customMatchmaking.CreateRoom();
            HandleOperationResult(result, "Room created.");
        }
        catch (System.Exception exception)
        {
            SetBusy(false, $"Create room failed: {exception.Message}");
            Debug.LogException(exception);
        }
    }

    public async void JoinRoom()
    {
        if (!CanUseMatchmaking())
        {
            return;
        }

        string roomId = joinRoomInput != null ? joinRoomInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(roomId))
        {
            SetStatus("Enter a Room ID first.");
            return;
        }

        try
        {
            SetBusy(true, $"Joining {roomId}...");
            CustomMatchmaking.RoomOperationResult result = await customMatchmaking.JoinRoom(roomId, null);
            HandleOperationResult(result, "Joined room.");
        }
        catch (System.Exception exception)
        {
            SetBusy(false, $"Join room failed: {exception.Message}");
            Debug.LogException(exception);
        }
    }

    public void CopyRoomId()
    {
        if (string.IsNullOrWhiteSpace(currentRoomId))
        {
            SetStatus("No Room ID to copy yet.");
            return;
        }

        GUIUtility.systemCopyBuffer = currentRoomId;
        SetStatus($"Copied Room ID: {currentRoomId}");
    }

    public void StartGame()
    {
        if (lobbySceneStart == null)
        {
            lobbySceneStart = gameObject.AddComponent<LobbyNetworkSceneStart>();
        }

        lobbySceneStart.ConfigureGameScene(gameSceneBuildIndex);
        lobbySceneStart.StartGameForRoom();
    }

    private bool CanUseMatchmaking()
    {
        if (busy)
        {
            return false;
        }

        if (customMatchmaking != null)
        {
            return true;
        }

        customMatchmaking = FindFirstObjectByType<CustomMatchmaking>();
        if (customMatchmaking != null)
        {
            return true;
        }

        SetStatus("Custom Matchmaking block was not found in StartScene.");
        return false;
    }

    private void HandleOperationResult(CustomMatchmaking.RoomOperationResult result, string successMessage)
    {
        SetBusy(false, result.IsSuccess ? successMessage : result.ErrorMessage);

        if (!result.IsSuccess)
        {
            return;
        }

        currentRoomId = result.RoomToken ?? string.Empty;
        roomCreated = true;

        if (joinRoomInput != null && string.IsNullOrWhiteSpace(joinRoomInput.text))
        {
            joinRoomInput.text = currentRoomId;
        }

        if (createButton != null)
        {
            createButton.interactable = false;
        }

        if (joinButton != null)
        {
            joinButton.interactable = false;
        }

        UpdateStatus();
    }

    private void SetBusy(bool value, string message)
    {
        busy = value;
        SetButtonsInteractable(!busy);
        SetStatus(message);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createButton != null) createButton.interactable = interactable && !roomCreated;
        if (joinButton != null) joinButton.interactable = interactable && !roomCreated;
        if (copyButton != null) copyButton.interactable = interactable;
        if (startButton != null) startButton.interactable = interactable;
    }

    private void UpdateStatus()
    {
        NetworkRunner runner = GetActiveRunner();
        if (runner != null)
        {
            currentRoomId = runner.SessionInfo?.Name ?? currentRoomId;
        }
        else if (customMatchmaking != null && customMatchmaking.IsConnected)
        {
            currentRoomId = customMatchmaking.ConnectedRoomToken;
        }

        if (roomIdText != null)
        {
            roomIdText.text = string.IsNullOrWhiteSpace(currentRoomId) ? "Room ID: -" : $"Room ID: {currentRoomId}";
        }

        if (playerCountText != null)
        {
            int players = runner != null ? CountPlayers(runner) : 0;
            playerCountText.text = $"Players: {players}";
        }

        if (copyButton != null)
        {
            copyButton.interactable = !busy && !string.IsNullOrWhiteSpace(currentRoomId);
        }

        if (startButton != null)
        {
            startButton.interactable = !busy && runner != null && runner.IsRunning && runner.IsSharedModeMasterClient;
        }
        if (createButton != null)
        {
            createButton.interactable = !busy && !roomCreated;
        }
        if (joinButton != null)
        {
            joinButton.interactable = !busy && !roomCreated;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = string.IsNullOrWhiteSpace(message) ? "Ready." : message;
        }
    }

    private static NetworkRunner GetActiveRunner()
    {
        for (int i = NetworkRunner.Instances.Count - 1; i >= 0; i--)
        {
            NetworkRunner runner = NetworkRunner.Instances[i];
            if (runner != null && runner.IsRunning)
            {
                return runner;
            }
        }

        return null;
    }

    private static int CountPlayers(NetworkRunner runner)
    {
        int count = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    private void ResolveMissingReferences()
    {
        if (customMatchmaking == null)
        {
            customMatchmaking = FindFirstObjectByType<CustomMatchmaking>();
        }

        if (lobbySceneStart == null)
        {
            lobbySceneStart = FindFirstObjectByType<LobbyNetworkSceneStart>();
        }

        if (lobbySceneStart == null)
        {
            lobbySceneStart = gameObject.AddComponent<LobbyNetworkSceneStart>();
        }

        if (createButton == null)
        {
            createButton = FindButtonByName("CreatRoomButton", "CreateRoomButton");
        }

        if (joinButton == null)
        {
            joinButton = FindButtonByName("JoinButton");
        }

        if (joinRoomInput == null)
        {
            joinRoomInput = FindInputByName("Room ID", "Join Room ID");
        }

        SetStatus("Create a room or join with Room ID.");
        UpdateStatus();
    }

    private void RegisterRoomIdInputKeyboard()
    {
        if (joinRoomInput == null)
        {
            return;
        }

        joinRoomInput.onSelect.RemoveListener(OpenRoomIdKeyboard);
        joinRoomInput.onSelect.AddListener(OpenRoomIdKeyboard);

        roomIdEventTrigger = joinRoomInput.GetComponent<EventTrigger>();
        if (roomIdEventTrigger == null)
        {
            roomIdEventTrigger = joinRoomInput.gameObject.AddComponent<EventTrigger>();
        }

        roomIdPointerClickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        roomIdPointerClickEntry.callback.AddListener(_ => OpenRoomIdKeyboard(joinRoomInput.text));
        roomIdEventTrigger.triggers.Add(roomIdPointerClickEntry);
    }

    private void OpenRoomIdKeyboard(string _)
    {
        if (joinRoomInput == null || !joinRoomInput.interactable || roomCreated)
        {
            return;
        }

        if (!TouchScreenKeyboard.isSupported)
        {
            return;
        }

        joinRoomInput.ActivateInputField();
        roomIdKeyboard = TouchScreenKeyboard.Open(
            joinRoomInput.text,
            TouchScreenKeyboardType.Default,
            false,
            false,
            false,
            false,
            "Input Room ID");
    }

    private void SyncRoomIdKeyboard()
    {
        if (roomIdKeyboard == null)
        {
            return;
        }

        if (roomIdKeyboard.status == TouchScreenKeyboard.Status.Visible)
        {
            ApplyRoomIdKeyboardText();
            return;
        }

        if (roomIdKeyboard.status == TouchScreenKeyboard.Status.Done)
        {
            ApplyRoomIdKeyboardText();
            roomIdKeyboard = null;
            return;
        }

        if (roomIdKeyboard.status == TouchScreenKeyboard.Status.Canceled ||
            roomIdKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
        {
            roomIdKeyboard = null;
        }
    }

    private void ApplyRoomIdKeyboardText()
    {
        if (joinRoomInput == null || roomIdKeyboard == null)
        {
            return;
        }

        if (joinRoomInput.text == roomIdKeyboard.text)
        {
            return;
        }

        joinRoomInput.SetTextWithoutNotify(roomIdKeyboard.text);
        joinRoomInput.ForceLabelUpdate();
    }

    private static Button FindButtonByName(params string[] names)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            foreach (string buttonName in names)
            {
                if (button.name == buttonName)
                {
                    return button;
                }
            }
        }

        return null;
    }

    private static TMP_InputField FindInputByName(params string[] names)
    {
        TMP_InputField[] inputs = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_InputField input in inputs)
        {
            foreach (string inputName in names)
            {
                if (input.name == inputName)
                {
                    return input;
                }
            }
        }

        return null;
    }
}
