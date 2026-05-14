public static class GameOverPayload
{
    public static bool HasData { get; private set; }
    public static bool PlayerWon { get; private set; }
    public static bool TimeUp { get; private set; }
    public static string Title { get; private set; }
    public static string Body { get; private set; }

    public static void Set(bool playerWon, bool timeUp, string title, string body)
    {
        HasData = true;
        PlayerWon = playerWon;
        TimeUp = timeUp;
        Title = title;
        Body = body;
    }

    public static void Clear()
    {
        HasData = false;
        PlayerWon = false;
        TimeUp = false;
        Title = string.Empty;
        Body = string.Empty;
    }
}
