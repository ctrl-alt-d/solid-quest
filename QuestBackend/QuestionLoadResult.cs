namespace QuestBackend;

public sealed record QuestionLoadResult(bool Success, QuestMetadata? Metadata, IReadOnlyList<Question> Questions, string ErrorMessage)
{
    public static QuestionLoadResult Succeeded(QuestMetadata metadata, IReadOnlyList<Question> questions) 
        => new(true, metadata, questions, string.Empty);

    public static QuestionLoadResult Failed(string errorMessage) 
        => new(false, null, [], errorMessage);
}
