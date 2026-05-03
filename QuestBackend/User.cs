namespace QuestBackend;

public class User
{
    public string UserName { get; set; } = null!;
    public string RestoreToken { get; set; } = string.Empty;
    public int Score { get; set; }
    public long TotalMilliseconds { get; set; }
    public bool IsAdmin { get; set; }

}
