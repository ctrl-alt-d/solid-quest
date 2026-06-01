namespace QuestBackend;

public sealed class Users : IUsers
{
    private readonly Dictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, User> _usersByRestoreToken = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public bool TryAdd(string userName, bool isAdmin, out string? errorMessage)
        => TryAdd(userName, isAdmin, out _, out errorMessage);

    public bool TryAdd(string userName, bool isAdmin, out User? user, out string? errorMessage)
    {
        var normalizedUserName = Normalize(userName);

        lock (_lock)
        {
            if (_users.ContainsKey(normalizedUserName))
            {
                user = null;
                errorMessage = $"Username '{userName}' is already taken.";
                return false;
            }

            user = new User
            {
                UserName = userName,
                RestoreToken = CreateRestoreToken(),
                Score = 0,
                TotalMilliseconds = 0,
                IsAdmin = isAdmin
            };

            _users[normalizedUserName] = user;
            _usersByRestoreToken[user.RestoreToken] = user;
        }

        errorMessage = null;
        return true;
    }

    public void Remove(string userName)
    {
        var normalizedUserName = Normalize(userName);

        lock (_lock)
        {
            if (_users.Remove(normalizedUserName, out var user)
                && !string.IsNullOrWhiteSpace(user.RestoreToken))
            {
                _usersByRestoreToken.Remove(user.RestoreToken);
                user.RestoreToken = string.Empty;
            }
        }
    }

    public bool Contains(string userName)
    {
        lock (_lock)
        {
            return _users.ContainsKey(Normalize(userName));
        }
    }

    public bool TryGetByUserName(string userName, out User? user)
    {
        lock (_lock)
        {
            return _users.TryGetValue(Normalize(userName), out user);
        }
    }

    public bool TryGetByRestoreToken(string restoreToken, out User? user)
    {
        lock (_lock)
        {
            return _usersByRestoreToken.TryGetValue(restoreToken.Trim(), out user);
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

    private string CreateRestoreToken()
    {
        string restoreToken;

        do
        {
            restoreToken = Guid.NewGuid().ToString("N");
        }
        while (_usersByRestoreToken.ContainsKey(restoreToken));

        return restoreToken;
    }
}
