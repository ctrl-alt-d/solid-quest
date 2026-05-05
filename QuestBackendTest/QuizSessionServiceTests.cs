using Microsoft.Extensions.Options;
using QuestBackend;

namespace QuestBackendTest;

public sealed class QuizSessionServiceTests
{
    [Fact]
    public void TryJoin_RequiresUniqueUserNames_AndReservesAdminForAdminLogin()
    {
        var session = CreateSession(adminUserName: "moderator");

        var firstJoin = session.TryJoin("Alice", isAdmin: false, out var firstUser, out var firstError);
        var duplicateJoin = session.TryJoin(" Alice ", isAdmin: false, out var duplicateError);
        var reservedAdminJoin = session.TryJoin("moderator", isAdmin: false, out var reservedAdminError);
        var adminJoin = session.TryJoin("moderator", isAdmin: true, out var adminUser, out var adminError);
        var snapshot = session.GetSnapshot();

        Assert.True(firstJoin);
        Assert.NotNull(firstUser);
        Assert.True(Guid.TryParse(firstUser!.RestoreToken, out _));
        Assert.Equal(string.Empty, firstError);
        Assert.False(duplicateJoin);
        Assert.Contains("already taken", duplicateError);
        Assert.False(reservedAdminJoin);
        Assert.Equal("The username 'moderator' is reserved.", reservedAdminError);
        Assert.True(adminJoin);
        Assert.NotNull(adminUser);
        Assert.True(Guid.TryParse(adminUser!.RestoreToken, out _));
        Assert.Equal(string.Empty, adminError);
        Assert.True(snapshot.HasAdmin);
        Assert.Equal(new[] { "Alice" }, snapshot.EnrolledPlayers);
    }

    [Fact]
    public void TryRestoreUser_ReturnsExistingUser_ByRestoreToken()
    {
        var session = CreateSession();
        session.TryJoin("Alice", isAdmin: false, out var joinedUser, out _);

        var restored = session.TryRestoreUser(joinedUser!.RestoreToken, out var restoredUser);

        Assert.True(restored);
        Assert.NotNull(restoredUser);
        Assert.Equal("Alice", restoredUser!.UserName);
        Assert.Equal(joinedUser.RestoreToken, restoredUser.RestoreToken);
        Assert.False(restoredUser.IsAdmin);
    }

    [Fact]
    public void LeaveByRestoreToken_RemovesUser_AndInvalidatesRestoreToken()
    {
        var session = CreateSession();
        session.TryJoin("Alice", isAdmin: false, out var joinedUser, out _);

        session.LeaveByRestoreToken(joinedUser!.RestoreToken);

        Assert.False(session.TryRestoreUser(joinedUser.RestoreToken, out _));
        Assert.Empty(session.GetSnapshot().EnrolledPlayers);
    }

    [Fact]
    public async Task TryStartAsync_OpensFirstQuestion_ForAdmin()
    {
        var session = CreateSession(adminUserName: "moderator");
        session.TryJoin("moderator", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var result = await session.TryStartAsync("moderator", null, QuestionTimeoutSettings.DefaultSeconds);
        var snapshot = session.GetSnapshot("Alice");

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.ErrorMessage);
        Assert.Equal(QuizStage.QuestionOpen, snapshot.Stage);
        Assert.NotNull(snapshot.CurrentQuestion);
        Assert.Equal(1, snapshot.CurrentQuestion!.Number);
        Assert.Equal(2, snapshot.CurrentQuestion.Total);
        Assert.Equal("Una <strong>interfície</strong>", snapshot.CurrentQuestion.Text);
        Assert.Null(snapshot.CurrentQuestion.CorrectAnswer);
        Assert.Equal(0, snapshot.CurrentQuestion.Responses);
        Assert.Equal(1, snapshot.CurrentQuestion.TotalPlayers);
        Assert.Equal(QuestionTimeoutSettings.DefaultSeconds, snapshot.CurrentQuestion.TimeoutSeconds);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(QuestionTimeoutSettings.DefaultSeconds), snapshot.CurrentQuestion.DeadlineUtc);
    }

    [Fact]
    public async Task TrySubmitAnswer_RevealsResults_WhenAllPlayersAnswer()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

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

    [Theory]
    [InlineData(QuizStage.QuestionOpen)]
    [InlineData(QuizStage.QuestionResults)]
    [InlineData(QuizStage.Completed)]
    public async Task TryRestart_AdminResetsQuestState_FromAnyStartedStage(QuizStage targetStage)
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out var alice, out _);
        session.TryJoin("Bob", isAdmin: false, out var bob, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

        if (targetStage == QuizStage.QuestionOpen)
        {
            session.TrySubmitAnswer("Alice", 1, out _);
        }
        else if (targetStage == QuizStage.QuestionResults)
        {
            session.TrySubmitAnswer("Alice", 1, out _);
            session.TrySubmitAnswer("Bob", 2, out _);
        }
        else if (targetStage == QuizStage.Completed)
        {
            session.TrySubmitAnswer("Alice", 1, out _);
            session.TrySubmitAnswer("Bob", 2, out _);
            session.TryAdvance("admin", out _);
            session.TrySubmitAnswer("Alice", 1, out _);
            session.TrySubmitAnswer("Bob", 2, out _);
            session.TryAdvance("admin", out _);
        }

        var restarted = session.TryRestart("admin", out var errorMessage);
        var snapshot = session.GetSnapshot("Alice");

        Assert.True(restarted);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(QuizStage.Enrollment, snapshot.Stage);
        Assert.Null(snapshot.CurrentQuestion);
        Assert.True(snapshot.CanStart);
        Assert.Equal(2, snapshot.PlayerCount);
        Assert.Equal(["Alice", "Bob"], snapshot.EnrolledPlayers);
        Assert.Equal(
            [
                new LeaderboardEntry("Alice", 0, 0),
                new LeaderboardEntry("Bob", 0, 0)
            ],
            snapshot.Leaderboard);
        Assert.True(session.TryRestoreUser(alice!.RestoreToken, out var restoredAlice));
        Assert.True(session.TryRestoreUser(bob!.RestoreToken, out var restoredBob));
        Assert.Equal("Alice", restoredAlice!.UserName);
        Assert.Equal("Bob", restoredBob!.UserName);
    }

    [Fact]
    public async Task TryRestart_NonAdmin_IsRejected()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

        var restarted = session.TryRestart("Alice", out var errorMessage);
        var snapshot = session.GetSnapshot("Alice");

        Assert.False(restarted);
        Assert.Equal("Only admin can restart the session.", errorMessage);
        Assert.Equal(QuizStage.QuestionOpen, snapshot.Stage);
        Assert.NotNull(snapshot.CurrentQuestion);
    }

    [Fact]
    public async Task ResultsSnapshot_UpdatesLeaderboard_ByCorrectAnswers()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);
        session.TrySubmitAnswer("Alice", 1, out _);
        session.TrySubmitAnswer("Bob", 2, out _);

        var snapshot = session.GetSnapshot();

        Assert.Equal(
            [
                new LeaderboardEntry("Alice", 1, 0),
                new LeaderboardEntry("Bob", 0, 0)
            ],
            snapshot.Leaderboard);
    }

    [Fact]
    public async Task TrySubmitAnswer_AccumulatesElapsedMilliseconds_OnAcceptedAnswers()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession(timeProvider: timeProvider);
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1200));
        var firstAccepted = session.TrySubmitAnswer("Alice", 1, out var firstError);
        session.TryAdvance("admin", out _);
        timeProvider.Advance(TimeSpan.FromMilliseconds(800));
        var secondAccepted = session.TrySubmitAnswer("Alice", 2, out var secondError);
        var snapshot = session.GetSnapshot();

        Assert.True(firstAccepted);
        Assert.Equal(string.Empty, firstError);
        Assert.True(secondAccepted);
        Assert.Equal(string.Empty, secondError);
        Assert.Equal(2000, Assert.Single(snapshot.Leaderboard).TotalMilliseconds);
    }

    [Fact]
    public async Task TrySubmitAnswer_DuplicateAnswer_DoesNotAccumulateTimeTwice()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession(timeProvider: timeProvider);
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

        timeProvider.Advance(TimeSpan.FromMilliseconds(500));
        var firstAccepted = session.TrySubmitAnswer("Alice", 1, out var firstError);
        timeProvider.Advance(TimeSpan.FromMilliseconds(700));
        var duplicateAccepted = session.TrySubmitAnswer("Alice", 2, out var duplicateError);
        var snapshot = session.GetSnapshot();

        Assert.True(firstAccepted);
        Assert.Equal(string.Empty, firstError);
        Assert.False(duplicateAccepted);
        Assert.Equal("You already answered this question.", duplicateError);
        Assert.Equal(500, snapshot.Leaderboard.Single(entry => entry.UserName == "Alice").TotalMilliseconds);
    }

    [Fact]
    public async Task Leaderboard_BreaksScoreTies_ByLowerTotalMilliseconds()
    {
        var timeProvider = new ManualTimeProvider();
        var session = CreateSession(timeProvider: timeProvider);
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);
        session.TryJoin("Bob", isAdmin: false, out _);
        await session.TryStartAsync("admin", null, QuestionTimeoutSettings.DefaultSeconds);

        timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        session.TrySubmitAnswer("Alice", 1, out _);
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));
        session.TrySubmitAnswer("Bob", 1, out _);
        var snapshot = session.GetSnapshot();

        Assert.Equal(
            [
                new LeaderboardEntry("Alice", 1, 400),
                new LeaderboardEntry("Bob", 1, 900)
            ],
            snapshot.Leaderboard);
    }

    [Fact]
    public async Task TryStartAsync_ReturnsLoadError_WhenQuestionSourceFails()
    {
        var session = CreateSession(questionLoader: new StubQuestionLoader(QuestionLoadResult.Failed("Bad YAML.")));
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var result = await session.TryStartAsync("admin", "https://example.com/questions.yaml", QuestionTimeoutSettings.DefaultSeconds);

        Assert.False(result.Success);
        Assert.Equal("Bad YAML.", result.ErrorMessage);
        Assert.Equal(QuizStage.Enrollment, session.GetSnapshot().Stage);
    }

    [Fact]
    public async Task TryStartAsync_UsesUrlLoader_WhenUrlProvided()
    {
        var loadedQuestions = new[]
        {
            new Question
            {
                Text = "Remote question",
                Answer1 = "A",
                Answer2 = "B",
                Answer3 = "C",
                Answer4 = "D",
                CorrectAnswer = 2,
                Explanation = "Why"
            }
        };

        var loader = new StubQuestionLoader(QuestionLoadResult.Succeeded(loadedQuestions));
        var session = CreateSession(questionLoader: loader);
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var result = await session.TryStartAsync("admin", "https://example.com/questions.yaml", QuestionTimeoutSettings.DefaultSeconds);
        var snapshot = session.GetSnapshot("Alice");

        Assert.True(result.Success);
        Assert.Equal("https://example.com/questions.yaml", loader.LoadedUrl);
        Assert.Equal("Remote question", snapshot.CurrentQuestion!.Text);
    }

    [Fact]
    public async Task TryStartAsync_UsesCustomTimeout_ForCurrentQuest()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var result = await session.TryStartAsync("admin", null, 45);
        var snapshot = session.GetSnapshot("Alice");

        Assert.True(result.Success);
        Assert.Equal(45, snapshot.CurrentQuestion!.TimeoutSeconds);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(45), snapshot.CurrentQuestion.DeadlineUtc);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public async Task TryStartAsync_RejectsInvalidTimeout(int timeoutSeconds)
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var result = await session.TryStartAsync("admin", null, timeoutSeconds);

        Assert.False(result.Success);
        Assert.Equal($"Question timeout must be between {QuestionTimeoutSettings.MinSeconds} and {QuestionTimeoutSettings.MaxSeconds} seconds.", result.ErrorMessage);
        Assert.Equal(QuizStage.Enrollment, session.GetSnapshot().Stage);
    }

    [Fact]
    public async Task TryStartAsync_RevealsResults_WhenCustomTimeoutExpires()
    {
        var session = CreateSession();
        session.TryJoin("admin", isAdmin: true, out _);
        session.TryJoin("Alice", isAdmin: false, out _);

        var started = await session.TryStartAsync("admin", null, QuestionTimeoutSettings.MinSeconds);

        Assert.True(started.Success);

        await Task.Delay(TimeSpan.FromSeconds(QuestionTimeoutSettings.MinSeconds + 1));

        var snapshot = session.GetSnapshot("Alice");
        Assert.Equal(QuizStage.QuestionResults, snapshot.Stage);
        Assert.Null(snapshot.CurrentQuestion!.TimeoutSeconds);
        Assert.Null(snapshot.CurrentQuestion.DeadlineUtc);
    }

    private static QuizSessionService CreateSession(string? adminUserName = null, TimeProvider? timeProvider = null, IQuestionLoader? questionLoader = null)
        => new(
            new Users(),
            questionLoader ?? new StubQuestionLoader(CreateSampleQuestionLoadResult()),
            timeProvider ?? new ManualTimeProvider(),
            Options.Create(new QuestOptions { AdminUserName = adminUserName ?? QuestOptions.DefaultAdminUserName }));

    private static QuestionLoadResult CreateSampleQuestionLoadResult()
        => QuestionLoadResult.Succeeded(
        [
            new Question
            {
                Text = "Una <strong>interfície</strong>",
                Answer1 = "És un contracte que defineix <strong>mètodes</strong> i propietats a implementar",
                Answer2 = "Es pot instanciar amb <code>new()</code>",
                Answer3 = "No pot implementar altres interfícies",
                Answer4 = ".NET no té interfícies",
                CorrectAnswer = 1,
                Explanation = "<p>Explicació 1</p>"
            },
            new Question
            {
                Text = "Una classe abstracta <strong>NO</strong> pot",
                Answer1 = "Instanciar-se amb <code>new()</code>",
                Answer2 = "Heretar d'altres classes",
                Answer3 = "Implementar interfícies",
                Answer4 = "Tenir mètodes abstractes i implementats",
                CorrectAnswer = 1,
                Explanation = "<p>Explicació 2</p>"
            }
        ]);

    private sealed class StubQuestionLoader(QuestionLoadResult result) : IQuestionLoader
    {
        public string? LoadedUrl { get; private set; }

        public Task<QuestionLoadResult> LoadSampleQuestionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        public Task<QuestionLoadResult> LoadQuestionsFromUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            LoadedUrl = url;
            return Task.FromResult(result);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
    }
}
