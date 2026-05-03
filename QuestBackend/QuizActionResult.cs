namespace QuestBackend;

public sealed record QuizActionResult(bool Success, string ErrorMessage)
{
    public static QuizActionResult Succeeded() => new(true, string.Empty);

    public static QuizActionResult Failed(string errorMessage) => new(false, errorMessage);
}
