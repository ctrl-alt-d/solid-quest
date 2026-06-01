namespace QuestBackend;

public interface IUsers
{
    bool TryAdd(string userName, bool isAdmin, out string? errorMessage);
    bool TryAdd(string userName, bool isAdmin, out User? user, out string? errorMessage);
    void Remove(string userName);
    bool Contains(string userName);
    bool TryGetByUserName(string userName, out User? user);
    bool TryGetByRestoreToken(string restoreToken, out User? user);
    int PlayerCount { get; }
    bool HasAdmin { get; }
    IReadOnlyList<string> GetPlayerNames();
    IReadOnlyList<LeaderboardEntry> GetLeaderboard();
    void ResetScores();
    void AddScore(string userName, int points);
    void AddAnswerTime(string userName, long elapsedMilliseconds);
}
