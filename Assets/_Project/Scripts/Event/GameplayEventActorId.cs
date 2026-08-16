using Fusion;

public static class GameplayEventActorId
{
    public static string FromRunner(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning)
            return "Player";

        return FromPlayerRef(runner.LocalPlayer);
    }

    public static string FromPlayerRef(PlayerRef player)
    {
        return player == PlayerRef.None
            ? "Host"
            : $"Player_{player.PlayerId}";
    }
}
