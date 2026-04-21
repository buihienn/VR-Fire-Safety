using UnityEngine;

public static class GameSettings
{
    private const string ShowGasLevelKey = "ShowGasLevel";

    public static bool ShowGasLevel
    {
        get => PlayerPrefs.GetInt(ShowGasLevelKey, 0) == 1;
        set => PlayerPrefs.SetInt(ShowGasLevelKey, value ? 1 : 0);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}