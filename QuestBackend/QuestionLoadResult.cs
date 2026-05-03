namespace QuestBackend;

public sealed record QuestionLoadResult(bool Success, IReadOnlyList<Question> Questions, string ErrorMessage)
{
    public static QuestionLoadResult Succeeded(IReadOnlyList<Question> questions) => new(true, questions, string.Empty);

    public static QuestionLoadResult Failed(string errorMessage) => new(false, [], errorMessage);
}
