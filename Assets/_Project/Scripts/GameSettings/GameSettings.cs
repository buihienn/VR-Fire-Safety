using UnityEngine;

public enum DayNightSetting
{
    Day = 0,
    Night = 1
}

public static class GameSettings
{
    private const string ShowGasLevelKey = "ShowGasLevel";
    private const string ShowTimeKey = "ShowTime";
    private const string DayNightKey = "DayNight";

    public static bool ShowGasLevel
    {
        get => PlayerPrefs.GetInt(ShowGasLevelKey, 0) == 1;
        set => PlayerPrefs.SetInt(ShowGasLevelKey, value ? 1 : 0);
    }

    public static bool ShowTime
    {
        get => PlayerPrefs.GetInt(ShowTimeKey, 1) == 1;
        set => PlayerPrefs.SetInt(ShowTimeKey, value ? 1 : 0);
    }

    public static DayNightSetting DayNight
    {
        get => PlayerPrefs.GetInt(DayNightKey, (int)DayNightSetting.Day) ==
               (int)DayNightSetting.Night
            ? DayNightSetting.Night
            : DayNightSetting.Day;
        set => PlayerPrefs.SetInt(DayNightKey, (int)value);
    }

    public static bool IsNight => DayNight == DayNightSetting.Night;

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
