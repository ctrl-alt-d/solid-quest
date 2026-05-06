namespace QuestBackend;

public sealed record QuizSessionSnapshot(
    QuizStage Stage,
    bool HasAdmin,
    bool CanStart,
    bool CanLoad,
    int PlayerCount,
    IReadOnlyList<string> EnrolledPlayers,
    QuestMetadata? QuestMetadata,
    QuestionView? CurrentQuestion,
    IReadOnlyList<LeaderboardEntry> Leaderboard);
