namespace QuestUI.Auth;

public sealed class PlayerSession
{
    public string UserName { get; private set; } = string.Empty;

    public string RestoreToken { get; private set; } = string.Empty;

    public bool IsAdmin { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserName);

    public void Set(string userName, bool isAdmin, string restoreToken)
    {
        UserName = userName;
        IsAdmin = isAdmin;
        RestoreToken = restoreToken;
    }

    public void Clear()
    {
        UserName = string.Empty;
        RestoreToken = string.Empty;
        IsAdmin = false;
    }
}
