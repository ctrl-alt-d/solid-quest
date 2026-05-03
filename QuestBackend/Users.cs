namespace QuestBackend;

public sealed class Users
{
    private readonly Dictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public bool TryAdd(string userName, bool isAdmin, out string? errorMessage)
    {
        var normalizedUserName = Normalize(userName);

        lock (_lock)
        {
            if (_users.ContainsKey(normalizedUserName))
            {
                errorMessage = $"Username '{userName}' is already taken.";
                return false;
            }

            _users[normalizedUserName] = new User
            {
                UserName = userName,
                Score = 0,
                TotalMilliseconds = 0,
                IsAdmin = isAdmin
            };
        }

        errorMessage = null;
        return true;
    }

    public void Remove(string userName)
    {
        var normalizedUserName = Normalize(userName);

        lock (_lock)
        {
            _users.Remove(normalizedUserName);
        }
    }

    public bool Contains(string userName)
    {
        lock (_lock)
        {
            return _users.ContainsKey(Normalize(userName));
        }
    }

    public int PlayerCount
    {
        get
        {
            lock (_lock)
            {
                return _users.Values.Count(user => !user.IsAdmin);
            }
        }
    }

    public bool HasAdmin
    {
        get
        {
            lock (_lock)
            {
                return _users.Values.Any(user => user.IsAdmin);
            }
        }
    }

    public IReadOnlyList<string> GetPlayerNames()
    {
        lock (_lock)
        {
            return _users.Values
                .Where(user => !user.IsAdmin)
                .Select(user => user.UserName)
                .OrderBy(userName => userName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<LeaderboardEntry> GetLeaderboard()
    {
        lock (_lock)
        {
            return _users.Values
                .Where(user => !user.IsAdmin)
                .OrderByDescending(user => user.Score)
                .ThenBy(user => user.TotalMilliseconds)
                .ThenBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(user => new LeaderboardEntry(user.UserName, user.Score, user.TotalMilliseconds))
                .ToList();
        }
    }

    public void ResetScores()
    {
        lock (_lock)
        {
            foreach (var user in _users.Values.Where(user => !user.IsAdmin))
            {
                user.Score = 0;
                user.TotalMilliseconds = 0;
            }
        }
    }

    public void AddScore(string userName, int points)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(Normalize(userName), out var user) && !user.IsAdmin)
            {
                user.Score += points;
            }
        }
    }

    public void AddAnswerTime(string userName, long elapsedMilliseconds)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(Normalize(userName), out var user) && !user.IsAdmin)
            {
                user.TotalMilliseconds += elapsedMilliseconds;
            }
        }
    }

    private static string Normalize(string userName) => userName.Trim();
}
