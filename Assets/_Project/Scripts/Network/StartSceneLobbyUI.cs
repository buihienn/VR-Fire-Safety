using Fusion;
using Meta.XR.MultiplayerBlocks.Shared;
using TMPro;
using UnityEngine;
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

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void Update()
    {
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
        if (joinButton != null) joinButton.interactable = interactable;
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

        SetStatus("Create a room or join with Room ID.");
        UpdateStatus();
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
}
