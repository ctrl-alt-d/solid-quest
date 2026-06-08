namespace QuestBackend;

public sealed record AnswerOptionView(int Index, string Text, int Votes, bool IsCorrect);

public sealed record QuestionView(
    int Number,
    int Total,
    string Text,
    string? Image,
    string? ImageAlt,
    IReadOnlyList<AnswerOptionView> Answers,
    string Explanation,
    int? SelectedAnswer,
    int? CorrectAnswer,
    int Responses,
    int TotalPlayers,
    int? TimeoutSeconds,
    DateTimeOffset? DeadlineUtc,
    int Points,
    bool IsSurvey = false);
