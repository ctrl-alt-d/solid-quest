namespace QuestUI.Auth;

public sealed record LoginResult(bool Succeeded, string ErrorMessage)
{
    public static LoginResult Success() => new(true, string.Empty);

    public static LoginResult Failure(string errorMessage) => new(false, errorMessage);
}
