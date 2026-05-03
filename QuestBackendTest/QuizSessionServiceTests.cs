using QuestBackend;

namespace QuestBackendTest;

public sealed class QuizSessionServiceTests
{
    [Fact]
    public void TryJoin_RequiresUniqueUserNames_AndReservesAdminForAdminLogin()
    {
        var session = CreateSession();

        var firstJoin = session.TryJoin("Alice", isAdmin: false, out var firstError);
        var duplicateJoin = session.TryJoin(" Alice ", isAdmin: false, out var duplicateError);
        var reservedAdminJoin = session.TryJoin("admin", isAdmin: false, out var reservedAdminError);
        var adminJoin = session.TryJoin("admin", isAdmin: true, out var adminError);
        var snapshot = session.GetSnapshot();

        Assert.True(firstJoin);
        Assert.Equal(string.Empty, firstError);
        Assert.False(duplicateJoin);
        Assert.Contains("already taken", duplicateError);
        Assert.False(reservedAdminJoin);
        Assert.Equal("The username 'admin' is reserved.", reservedAdminError);
        Assert.True(adminJoin);
        Assert.Equal(string.Empty, adminError);
        Assert.True(snapshot.HasAdmin);
        Assert.Equal(new[] { "Alice" }, snapshot.EnrolledPlayers);
    }

    [Fact]
    public void TryStart_OpensFirstQuestion_ForAdmin()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var started = session.TryStart("admin", out var errorMessage);
        var snapshot = session.GetSnapshot("Alice");

        Assert.True(started);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(QuizStage.QuestionOpen, snapshot.Stage);
        Assert.NotNull(snapshot.CurrentQuestion);
        Assert.Equal(1, snapshot.CurrentQuestion!.Number);
        Assert.Equal(2, snapshot.CurrentQuestion.Total);
        Assert.Equal("Una interfície", snapshot.CurrentQuestion.Text);
        Assert.Null(snapshot.CurrentQuestion.CorrectAnswer);
        Assert.Equal(0, snapshot.CurrentQuestion.Responses);
        Assert.Equal(1, snapshot.CurrentQuestion.TotalPlayers);
    }

    [Fact]
    public void TrySubmitAnswer_RevealsResults_WhenAllPlayersAnswer()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        session.TryStart("admin", out _);

        var aliceAnswered = session.TrySubmitAnswer("Alice", 1, out var aliceError);
        var openSnapshot = session.GetSnapshot("Alice");
        var bobAnswered = session.TrySubmitAnswer("Bob", 2, out var bobError);
        var resultsSnapshot = session.GetSnapshot("Alice");

        Assert.True(aliceAnswered);
        Assert.Equal(string.Empty, aliceError);
        Assert.Equal(QuizStage.QuestionOpen, openSnapshot.Stage);
        Assert.Equal(1, openSnapshot.CurrentQuestion!.SelectedAnswer);
        Assert.Equal(1, openSnapshot.CurrentQuestion.Responses);

        Assert.True(bobAnswered);
        Assert.Equal(string.Empty, bobError);
        Assert.Equal(QuizStage.QuestionResults, resultsSnapshot.Stage);
        Assert.Equal(1, resultsSnapshot.CurrentQuestion!.CorrectAnswer);
        Assert.NotEmpty(resultsSnapshot.CurrentQuestion.Explanation);
        Assert.Equal(new[] { 1, 1, 0, 0 }, resultsSnapshot.CurrentQuestion.Answers.Select(answer => answer.Votes));
        Assert.Equal(new[] { true, false, false, false }, resultsSnapshot.CurrentQuestion.Answers.Select(answer => answer.IsCorrect));
    }

    [Fact]
    public void ResultsSnapshot_UpdatesLeaderboard_ByCorrectAnswers()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        session.TryStart("admin", out _);
        session.TrySubmitAnswer("Alice", 1, out _);
        session.TrySubmitAnswer("Bob", 2, out _);

        var snapshot = session.GetSnapshot();

        Assert.Equal(
            [
                new LeaderboardEntry("Alice", 1),
                new LeaderboardEntry("Bob", 0)
            ],
            snapshot.Leaderboard);
    }

    private static QuizSessionService CreateSession() => new(new Users(), new QuestionLoader());
}
