public static class DayNightPayload
{
    public static bool HasValue { get; private set; }
    public static bool IsNight { get; private set; }

    public static void Set(bool isNight)
    {
        HasValue = true;
        IsNight = isNight;
    }

    public static void Clear()
    {
        HasValue = false;
        IsNight = false;
    }
}
