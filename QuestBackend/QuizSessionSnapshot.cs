namespace QuestBackend;

public sealed record QuizSessionSnapshot(
    QuizStage Stage,
    bool HasAdmin,
    bool CanStart,
    int PlayerCount,
    IReadOnlyList<string> EnrolledPlayers,
    QuestionView? CurrentQuestion,
    IReadOnlyList<LeaderboardEntry> Leaderboard);
