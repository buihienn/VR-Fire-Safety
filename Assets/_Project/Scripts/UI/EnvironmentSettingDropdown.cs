using Oculus.Interaction.Samples;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(DropDownGroup))]
public class EnvironmentSettingDropdown : MonoBehaviour
{
    private const int DayIndex = 0;
    private const int NightIndex = 1;

    [Header("Dropdown")]
    [SerializeField] private DropDownGroup dropdownGroup;

    [Header("Options (resolved automatically when empty)")]
    [SerializeField] private Toggle dayToggle;
    [SerializeField] private Toggle nightToggle;

    private bool initialized;
    private bool listenerRegistered;

    private void Start()
    {
        ResolveReferences();

        initialized = true;
        RegisterListener();
        RefreshFromSettings();

        Debug.Log(
            $"[{nameof(EnvironmentSettingDropdown)}] Initialized | " +
            $"GameSettings.DayNight={GameSettings.DayNight} | " +
            $"DropDownGroup.SelectedIndex={dropdownGroup?.SelectedIndex ?? -1}",
            this);
    }

    private void OnEnable()
    {
        if (!initialized)
            return;

        RegisterListener();
        RefreshFromSettings();
    }

    private void OnDisable()
    {
        UnregisterListener();
    }

    public void RefreshFromSettings()
    {
        Toggle selectedToggle = GameSettings.IsNight ? nightToggle : dayToggle;

        if (selectedToggle == null)
        {
            Debug.LogWarning(
                $"{nameof(EnvironmentSettingDropdown)}: Toggle for {GameSettings.DayNight} was not found.",
                this);
            return;
        }

        if (!selectedToggle.isOn)
            selectedToggle.isOn = true;
    }

    public void HandleSelectionChanged(int selectedIndex)
    {
        DayNightSetting previousSetting = GameSettings.DayNight;
        DayNightSetting setting;

        switch (selectedIndex)
        {
            case DayIndex:
                setting = DayNightSetting.Day;
                break;
            case NightIndex:
                setting = DayNightSetting.Night;
                break;
            default:
                Debug.LogWarning(
                    $"{nameof(EnvironmentSettingDropdown)}: Unsupported dropdown index {selectedIndex}.",
                    this);
                return;
        }

        Debug.Log(
            $"[{nameof(EnvironmentSettingDropdown)}] Dropdown selected | " +
            $"Index={selectedIndex} | PreviousGameSetting={previousSetting} | RequestedGameSetting={setting}",
            this);

        if (previousSetting == setting)
        {
            Debug.Log(
                $"[{nameof(EnvironmentSettingDropdown)}] GameSettings unchanged because it is already {setting}.",
                this);
            return;
        }

        GameSettings.DayNight = setting;
        GameSettings.Save();

        DayNightSetting savedSetting = GameSettings.DayNight;
        Debug.Log(
            $"[{nameof(EnvironmentSettingDropdown)}] GameSettings saved | " +
            $"Before={previousSetting} | After={savedSetting} | SaveVerified={savedSetting == setting}",
            this);
    }

    private void ResolveReferences()
    {
        if (dropdownGroup == null)
            dropdownGroup = GetComponent<DropDownGroup>();

        if (dayToggle == null)
            dayToggle = FindOptionToggle("DayToggle");

        if (nightToggle == null)
            nightToggle = FindOptionToggle("NightToggle");

        if (dropdownGroup == null)
        {
            Debug.LogError(
                $"{nameof(EnvironmentSettingDropdown)} requires a {nameof(DropDownGroup)} on the same object.",
                this);
        }
    }

    private void RegisterListener()
    {
        if (listenerRegistered || dropdownGroup == null)
            return;

        dropdownGroup.WhenSelectionChanged.AddListener(HandleSelectionChanged);
        listenerRegistered = true;
    }

    private void UnregisterListener()
    {
        if (!listenerRegistered || dropdownGroup == null)
            return;

        dropdownGroup.WhenSelectionChanged.RemoveListener(HandleSelectionChanged);
        listenerRegistered = false;
    }

    private Toggle FindOptionToggle(string objectName)
    {
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);

        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i].name == objectName)
                return toggles[i];
        }

        return null;
    }
}
