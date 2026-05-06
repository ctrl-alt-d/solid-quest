namespace QuestBackend;

public interface IQuizSessionService
{
    event Action? StateChanged;

    QuizSessionSnapshot GetSnapshot(string? userName = null);
    bool TryJoin(string userName, bool isAdmin, out string errorMessage);
    bool TryJoin(string userName, bool isAdmin, out User? user, out string errorMessage);
    bool TryRestoreUser(string restoreToken, out User? user);
    void Leave(string userName);
    void LeaveByRestoreToken(string restoreToken);
    Task<QuizActionResult> TryLoadQuestAsync(string userName, string? questionsUrl, CancellationToken cancellationToken = default);
    Task<QuizActionResult> TryStartAsync(string userName, int questionTimeoutSeconds, bool progressiveScoring, CancellationToken cancellationToken = default);
    bool TryRestart(string userName, out string errorMessage);
    bool TrySubmitAnswer(string userName, int answerIndex, out string errorMessage);
    bool TryAdvance(string userName, out string errorMessage);
    QuestionView? GetPreviewQuestion(string userName, int questionIndex);
    int GetQuestionCount();
}
