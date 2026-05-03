namespace QuestUI.Auth;

public sealed class PlayerSession
{
    public string UserName { get; private set; } = string.Empty;

    public bool IsAdmin { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserName);

    public void Set(string userName, bool isAdmin)
    {
        UserName = userName;
        IsAdmin = isAdmin;
    }

    public void Clear()
    {
        UserName = string.Empty;
        IsAdmin = false;
    }
}
