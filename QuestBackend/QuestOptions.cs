namespace QuestBackend;

public sealed class QuestOptions
{
    public const string SectionName = "Quest";
    public const string DefaultAdminUserName = "admin";

    private string _adminUserName = DefaultAdminUserName;

    public string AdminUserName
    {
        get => _adminUserName;
        set => _adminUserName = string.IsNullOrWhiteSpace(value)
            ? DefaultAdminUserName
            : value.Trim();
    }

    public bool IsAdminUserName(string? userName)
        => !string.IsNullOrWhiteSpace(userName)
           && string.Equals(userName.Trim(), AdminUserName, StringComparison.OrdinalIgnoreCase);
}
