using System.Text;
using Fusion;
using Meta.XR.MultiplayerBlocks.Fusion;
using Meta.XR.MultiplayerBlocks.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneLobbyUI : MonoBehaviour
{
    private const string PlayerNamePrefsKey = "VRFireSafety.PlayerName";
    private const int PlayerNameCharacterLimit = 24;

    [Header("Scene")]
    [SerializeField] private string gameSceneNameOrPath = "MainScene";
    [SerializeField] private int gameSceneBuildIndex = 4;

    [Header("Scene References")]
    [SerializeField] private CustomMatchmaking customMatchmaking;
    [SerializeField] private LobbyNetworkSceneStart lobbySceneStart;

    [Header("UI References")]
    [SerializeField] private TMP_Text roomIdText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerCountText;
    [NetworkPrefab]
    [SerializeField] private NetworkObject playerNameTagPrefab;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField joinRoomInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button startButton;

    private string currentRoomId = string.Empty;
    private bool busy;
    private bool roomCreated;
    private bool singleplayerStarting;
    private bool multiplayerSession;
    private bool nameTagSpawned;
    private TouchScreenKeyboard roomIdKeyboard;
    private TouchScreenKeyboard playerNameKeyboard;
    private EventTrigger roomIdEventTrigger;
    private EventTrigger.Entry roomIdPointerClickEntry;
    private EventTrigger playerNameEventTrigger;
    private EventTrigger.Entry playerNamePointerClickEntry;

    public static string CurrentPlayerName { get; private set; } = string.Empty;
    public bool IsMultiplayerSession => multiplayerSession;

    private void Awake()
    {
        ResolveMissingReferences();
        ResolveOrCreatePlayerNameInput();
        DisablePlatformAccountNameLookup();
        RegisterRoomIdInputKeyboard();
        RegisterPlayerNameInput();
        ApplyMultiplayerLobbyPhase();
    }

    private void OnDestroy()
    {
        if (joinRoomInput != null)
        {
            joinRoomInput.onSelect.RemoveListener(OpenRoomIdKeyboard);
            if (roomIdEventTrigger != null && roomIdPointerClickEntry != null)
            {
                roomIdEventTrigger.triggers.Remove(roomIdPointerClickEntry);
            }
        }

        if (playerNameInput != null)
        {
            playerNameInput.onSelect.RemoveListener(OpenPlayerNameKeyboard);
            playerNameInput.onValueChanged.RemoveListener(OnPlayerNameValueChanged);
            playerNameInput.onEndEdit.RemoveListener(OnPlayerNameEndEdit);
            if (playerNameEventTrigger != null && playerNamePointerClickEntry != null)
            {
                playerNameEventTrigger.triggers.Remove(playerNamePointerClickEntry);
            }
        }
    }

    private void Update()
    {
        SyncRoomIdKeyboard();
        SyncPlayerNameKeyboard();
        if (multiplayerSession)
        {
            TrySpawnPlayerNameTag();
        }
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
        if (!TryCommitPlayerName())
        {
            return;
        }

        if (!TrySpawnPlayerNameTag())
        {
            SetStatus("Waiting for the player name tag to connect...");
            return;
        }

        NetworkRunner runner = GetActiveRunner();
        if (!AreAllPlayersNamed(runner))
        {
            SetStatus("Waiting for every player to enter a Player Name...");
            return;
        }

        StartGameInternal();
    }

    public bool SetPlayerName(string playerName)
    {
        if (playerNameInput == null)
        {
            ResolveOrCreatePlayerNameInput();
        }

        if (playerNameInput == null)
        {
            SetStatus("Player Name input is unavailable.");
            return false;
        }

        playerNameInput.SetTextWithoutNotify(playerName ?? string.Empty);
        playerNameInput.ForceLabelUpdate();

        bool committed = TryCommitPlayerName();
        if (committed && multiplayerSession)
        {
            TrySpawnPlayerNameTag();
        }

        UpdateStatus();
        return committed;
    }

    private void StartGameInternal()
    {
        if (lobbySceneStart == null)
        {
            lobbySceneStart = gameObject.AddComponent<LobbyNetworkSceneStart>();
        }

        lobbySceneStart.ConfigureGameScene(gameSceneNameOrPath);
        lobbySceneStart.ConfigureGameScene(gameSceneBuildIndex);
        lobbySceneStart.StartGameForRoom();
    }

    public async void StartGameAsSingleplayer()
    {
        if (busy || singleplayerStarting)
        {
            return;
        }

        singleplayerStarting = true;
        multiplayerSession = false;
        SetBusy(true, "Starting local game...");

        NetworkRunner runner = null;
        try
        {
            runner = CreateLocalNetworkRunner();
            if (runner == null)
            {
                SetBusy(false, "Could not find the Fusion NetworkRunner template.");
                singleplayerStarting = false;
                return;
            }

            NetworkSceneInfo startSceneInfo = GetCurrentSceneInfo();
            StartGameResult result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                Scene = startSceneInfo
            });

            if (!result.Ok)
            {
                SetBusy(false, $"Could not start local game: {result.ShutdownReason}");
                Debug.LogError(
                    $"[{nameof(StartSceneLobbyUI)}] Fusion GameMode.Single failed: " +
                    $"{result.ShutdownReason}, {result.ErrorMessage}",
                    this);
                Destroy(runner.gameObject);
                singleplayerStarting = false;
                return;
            }

            SetBusy(false, "Local game ready.");
            singleplayerStarting = false;
            StartGameInternal();
        }
        catch (System.Exception exception)
        {
            SetBusy(false, $"Could not start local game: {exception.Message}");
            Debug.LogException(exception, this);
            if (runner != null)
            {
                Destroy(runner.gameObject);
            }

            singleplayerStarting = false;
        }
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
        multiplayerSession = true;
        ApplyMultiplayerLobbyPhase();
        TrySpawnPlayerNameTag();

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
        bool hasPlayerName = HasValidPlayerName();
        if (createButton != null) createButton.interactable = interactable && !roomCreated;
        if (joinButton != null) joinButton.interactable = interactable && !roomCreated;
        if (copyButton != null) copyButton.interactable = interactable;
        if (startButton != null) startButton.interactable = interactable && hasPlayerName;
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
            playerCountText.text = multiplayerSession
                ? $"Players: {players} | Named: {CountNamedPlayers(runner)}/{players}"
                : $"Players: {players}";
        }

        if (copyButton != null)
        {
            copyButton.interactable = !busy && !string.IsNullOrWhiteSpace(currentRoomId);
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(
                multiplayerSession &&
                runner != null &&
                runner.IsRunning &&
                runner.IsSharedModeMasterClient);
            startButton.interactable =
                !busy &&
                HasValidPlayerName() &&
                nameTagSpawned &&
                runner != null &&
                runner.IsRunning &&
                AreAllPlayersNamed(runner) &&
                runner.IsSharedModeMasterClient;
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

    private static bool AreAllPlayersNamed(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning)
        {
            return false;
        }

        PlayerNameTagFusion[] nameTags =
            FindObjectsByType<PlayerNameTagFusion>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!HasPlayerNameTag(runner, player, nameTags))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountNamedPlayers(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning)
        {
            return 0;
        }

        int namedPlayers = 0;
        PlayerNameTagFusion[] nameTags =
            FindObjectsByType<PlayerNameTagFusion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (HasPlayerNameTag(runner, player, nameTags))
            {
                namedPlayers++;
            }
        }

        return namedPlayers;
    }

    private static bool HasPlayerNameTag(
        NetworkRunner runner,
        PlayerRef player,
        PlayerNameTagFusion[] nameTags)
    {
        foreach (PlayerNameTagFusion nameTag in nameTags)
        {
            if (nameTag != null &&
                nameTag.Object != null &&
                nameTag.Object.Runner == runner &&
                nameTag.Object.InputAuthority == player &&
                !string.IsNullOrWhiteSpace(nameTag.OculusName.ToString()))
            {
                return true;
            }
        }

        return false;
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

        if (startButton == null)
        {
            startButton = FindButtonByName("StartButton");
        }

        if (joinRoomInput == null)
        {
            joinRoomInput = FindInputByName("Room ID", "Join Room ID");
        }

        if (playerNameInput == null)
        {
            playerNameInput = FindInputByName("Player Name", "PlayerNameInput");
        }

        SetStatus("Create a room or join with Room ID.");
        UpdateStatus();
    }

    private void ResolveOrCreatePlayerNameInput()
    {
        if (playerNameInput == null)
        {
            playerNameInput = FindInputByName("Player Name", "PlayerNameInput");
        }

        if (playerNameInput == null && joinRoomInput != null)
        {
            Transform joinRow = joinRoomInput.transform.parent;
            Transform inputList = joinRow != null ? joinRow.parent : null;
            if (inputList != null)
            {
                GameObject inputObject = Instantiate(joinRoomInput.gameObject, inputList, false);
                inputObject.name = "Player Name";
                inputObject.transform.SetSiblingIndex(joinRow.GetSiblingIndex());
                playerNameInput = inputObject.GetComponent<TMP_InputField>();

                if (inputObject.transform is RectTransform inputRect && joinRow is RectTransform joinRowRect)
                {
                    inputRect.sizeDelta = joinRowRect.sizeDelta;
                }
            }
        }

        if (playerNameInput == null)
        {
            Debug.LogError($"[{nameof(StartSceneLobbyUI)}] Could not create the Player Name input field.", this);
            return;
        }

        playerNameInput.characterLimit = PlayerNameCharacterLimit;
        playerNameInput.lineType = TMP_InputField.LineType.SingleLine;
        playerNameInput.contentType = TMP_InputField.ContentType.Standard;

        if (playerNameInput.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Enter Player Name";
        }

        string savedName = NormalizePlayerName(PlayerPrefs.GetString(PlayerNamePrefsKey, string.Empty));
        CurrentPlayerName = savedName;
        playerNameInput.SetTextWithoutNotify(savedName);
        playerNameInput.ForceLabelUpdate();
    }

    private void ApplyMultiplayerLobbyPhase()
    {
        if (playerNameInput != null)
        {
            playerNameInput.gameObject.SetActive(multiplayerSession);
        }

        if (createButton != null)
        {
            createButton.gameObject.SetActive(!multiplayerSession);
        }

        if (joinRoomInput != null && joinRoomInput.transform.parent != null)
        {
            joinRoomInput.transform.parent.gameObject.SetActive(!multiplayerSession);
        }
        else if (joinButton != null)
        {
            joinButton.gameObject.SetActive(!multiplayerSession);
        }

        if (startButton != null)
        {
            NetworkRunner runner = GetActiveRunner();
            bool canShowStart =
                multiplayerSession &&
                runner != null &&
                runner.IsRunning &&
                runner.IsSharedModeMasterClient;
            startButton.gameObject.SetActive(canShowStart);
        }
    }

    private static NetworkRunner CreateLocalNetworkRunner()
    {
        NetworkRunner[] runners =
            FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        NetworkRunner runnerTemplate = null;
        foreach (NetworkRunner candidate in runners)
        {
            if (candidate != null && !candidate.IsRunning)
            {
                runnerTemplate = candidate;
                break;
            }
        }

        if (runnerTemplate == null)
        {
            return null;
        }

        runnerTemplate.gameObject.SetActive(false);
        NetworkRunner localRunner = Instantiate(runnerTemplate);
        localRunner.name = "Single Player Runner";
        localRunner.gameObject.SetActive(true);
        DontDestroyOnLoad(localRunner.gameObject);
        return localRunner;
    }

    private static NetworkSceneInfo GetCurrentSceneInfo()
    {
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0 && activeScene.buildIndex < SceneManager.sceneCountInBuildSettings)
        {
            sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Additive);
        }

        return sceneInfo;
    }

    private void DisablePlatformAccountNameLookup()
    {
        PlayerNameTagSpawner[] platformNameSpawners =
            FindObjectsByType<PlayerNameTagSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerNameTagSpawner platformNameSpawner in platformNameSpawners)
        {
            platformNameSpawner.enabled = false;
        }

    }

    private void RegisterPlayerNameInput()
    {
        if (playerNameInput == null)
        {
            return;
        }

        playerNameInput.onSelect.RemoveListener(OpenPlayerNameKeyboard);
        playerNameInput.onSelect.AddListener(OpenPlayerNameKeyboard);
        playerNameInput.onValueChanged.RemoveListener(OnPlayerNameValueChanged);
        playerNameInput.onValueChanged.AddListener(OnPlayerNameValueChanged);
        playerNameInput.onEndEdit.RemoveListener(OnPlayerNameEndEdit);
        playerNameInput.onEndEdit.AddListener(OnPlayerNameEndEdit);

        playerNameEventTrigger = playerNameInput.GetComponent<EventTrigger>();
        if (playerNameEventTrigger == null)
        {
            playerNameEventTrigger = playerNameInput.gameObject.AddComponent<EventTrigger>();
        }

        playerNamePointerClickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        playerNamePointerClickEntry.callback.AddListener(_ => OpenPlayerNameKeyboard(playerNameInput.text));
        playerNameEventTrigger.triggers.Add(playerNamePointerClickEntry);
    }

    private void OpenPlayerNameKeyboard(string _)
    {
        if (playerNameInput == null || !playerNameInput.interactable)
        {
            return;
        }

        if (!TouchScreenKeyboard.isSupported)
        {
            return;
        }

        playerNameInput.ActivateInputField();
        playerNameKeyboard = TouchScreenKeyboard.Open(
            playerNameInput.text,
            TouchScreenKeyboardType.Default,
            false,
            false,
            false,
            false,
            "Input Player Name",
            PlayerNameCharacterLimit);
    }

    private void SyncPlayerNameKeyboard()
    {
        if (playerNameKeyboard == null)
        {
            return;
        }

        if (playerNameKeyboard.status == TouchScreenKeyboard.Status.Visible)
        {
            ApplyPlayerNameKeyboardText();
            return;
        }

        if (playerNameKeyboard.status == TouchScreenKeyboard.Status.Done)
        {
            ApplyPlayerNameKeyboardText();
            playerNameKeyboard = null;
            TryCommitPlayerName(false);
            return;
        }

        if (playerNameKeyboard.status == TouchScreenKeyboard.Status.Canceled ||
            playerNameKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
        {
            playerNameKeyboard = null;
        }
    }

    private void ApplyPlayerNameKeyboardText()
    {
        if (playerNameInput == null || playerNameKeyboard == null)
        {
            return;
        }

        string keyboardText = playerNameKeyboard.text;
        if (playerNameInput.text == keyboardText)
        {
            return;
        }

        playerNameInput.SetTextWithoutNotify(keyboardText);
        playerNameInput.ForceLabelUpdate();
    }

    private void OnPlayerNameValueChanged(string _)
    {
        UpdateStatus();
    }

    private void OnPlayerNameEndEdit(string _)
    {
        TryCommitPlayerName(false);
    }

    private bool TryCommitPlayerName(bool showValidationMessage = true)
    {
        string normalizedName = NormalizePlayerName(playerNameInput != null ? playerNameInput.text : string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            CurrentPlayerName = string.Empty;
            if (showValidationMessage)
            {
                SetStatus("Enter your Player Name before continuing.");
            }

            return false;
        }

        CurrentPlayerName = normalizedName;
        PlayerPrefs.SetString(PlayerNamePrefsKey, CurrentPlayerName);
        PlayerPrefs.Save();

        if (playerNameInput != null && playerNameInput.text != CurrentPlayerName)
        {
            playerNameInput.SetTextWithoutNotify(CurrentPlayerName);
            playerNameInput.ForceLabelUpdate();
        }

        ApplyPlayerNameToExistingTag();
        return true;
    }

    private bool HasValidPlayerName()
    {
        return !string.IsNullOrWhiteSpace(
            NormalizePlayerName(playerNameInput != null ? playerNameInput.text : CurrentPlayerName));
    }

    private bool TrySpawnPlayerNameTag()
    {
        if (nameTagSpawned)
        {
            return true;
        }

        if (!HasValidPlayerName())
        {
            return false;
        }

        NetworkRunner runner = GetActiveRunner();
        if (playerNameTagPrefab == null ||
            runner == null ||
            !runner.IsRunning ||
            runner.LocalPlayer == PlayerRef.None ||
            !runner.CanSpawn)
        {
            return false;
        }

        if (!TryCommitPlayerName(false))
        {
            return false;
        }

        try
        {
            NetworkObject spawnedNameTag = runner.Spawn(
                playerNameTagPrefab,
                Vector3.zero,
                Quaternion.identity,
                runner.LocalPlayer,
                (_, spawnedObject) =>
                {
                    PlayerNameTagFusion nameTag = spawnedObject.GetComponent<PlayerNameTagFusion>();
                    if (nameTag != null)
                    {
                        PlayerNameTagTrackingAnchor.Bind(nameTag);
                        nameTag.OculusName = CurrentPlayerName;
                    }
                },
                NetworkSpawnFlags.DontDestroyOnLoad);

            if (spawnedNameTag == null)
            {
                Debug.LogError($"[{nameof(StartSceneLobbyUI)}] Fusion returned no Player Name Tag object.", this);
                return false;
            }

            nameTagSpawned = true;
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[{nameof(StartSceneLobbyUI)}] Could not spawn the player name tag.", this);
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void ApplyPlayerNameToExistingTag()
    {
        if (!nameTagSpawned || string.IsNullOrWhiteSpace(CurrentPlayerName))
        {
            return;
        }

        PlayerNameTagFusion[] nameTags =
            FindObjectsByType<PlayerNameTagFusion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerNameTagFusion nameTag in nameTags)
        {
            if (nameTag != null && nameTag.Object != null && nameTag.Object.HasStateAuthority)
            {
                nameTag.OculusName = CurrentPlayerName;
            }
        }
    }

    private static string NormalizePlayerName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(Mathf.Min(rawName.Length, PlayerNameCharacterLimit));
        bool previousWasSpace = false;

        foreach (char character in rawName.Trim())
        {
            if (builder.Length >= PlayerNameCharacterLimit)
            {
                break;
            }

            if (char.IsControl(character) || character == '<' || character == '>')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (previousWasSpace || builder.Length == 0)
                {
                    continue;
                }

                builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString().TrimEnd();
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
