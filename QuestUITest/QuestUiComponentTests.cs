using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBackend;
using QuestUI.Auth;
using QuestUI.Components.Pages;
using QuestUI.Components.Quiz;

namespace QuestUITest;

public class QuestUiComponentTests : BunitContext
{
    [Fact]
    public void QuestionCardAppliesDistinctAnswerClasses()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Red", 2, false),
            new AnswerOptionView(2, "Blue", 1, false),
            new AnswerOptionView(3, "Green", 3, true),
            new AnswerOptionView(4, "Yellow", 0, false));

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => { })));

        var buttons = cut.FindAll("button.answer-button");

        buttons.Should().HaveCount(4);
        buttons[0].ClassList.Should().Contain("answer-1");
        buttons[1].ClassList.Should().Contain("answer-2");
        buttons[2].ClassList.Should().Contain("answer-3");
        buttons[3].ClassList.Should().Contain("answer-4");
    }

    [Fact]
    public void QuestionCardRendersQuestionAndAnswersAsHtmlMarkup()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Use <code>SRP</code>", 2, false),
            new AnswerOptionView(2, "Blue", 1, false),
            new AnswerOptionView(3, "Green", 3, true),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            Text = "What is <strong>SOLID</strong>?"
        };

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => { })));

        cut.Find("h2").InnerHtml.Should().Contain("<strong>SOLID</strong>");
        cut.Find("button.answer-button.answer-1 span:last-child").InnerHtml.Should().Contain("<code>SRP</code>");
        cut.Markup.Should().NotContain("&lt;strong&gt;SOLID&lt;/strong&gt;");
        cut.Markup.Should().NotContain("&lt;code&gt;SRP&lt;/code&gt;");
    }

    [Fact]
    public void QuestionCard_PlayerAnswersKeepVisibleLabelsAndAccessibleText()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Use <code>SRP</code>", 2, false),
            new AnswerOptionView(2, "Blue", 1, false),
            new AnswerOptionView(3, "Green", 3, true),
            new AnswerOptionView(4, "Yellow", 0, false));

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => { })));

        var buttons = cut.FindAll("button.answer-button");

        buttons.Should().HaveCount(4);
        var playerLabel = buttons[0].QuerySelector(".answer-option-label")!;

        playerLabel.TextContent.Should().Be("A · Red");
        playerLabel.ClassList.Should().Contain("answer-label-badge");
        buttons[0].QuerySelector(".player-answer-text")!.InnerHtml.Should().Contain("<code>SRP</code>");
        buttons[0].GetAttribute("aria-label").Should().Be("Answer A, Red: Use SRP");
        buttons[3].GetAttribute("aria-label").Should().Be("Answer D, Yellow: Yellow");
    }

    [Fact]
    public void QuestionCard_RendersCountdown_WhenDeadlineIsPresent()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, true),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            TimeoutSeconds = QuestionTimeoutSettings.DefaultSeconds,
            DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(25)
        };

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => { })));

        cut.Markup.Should().Contain("left");
        cut.Find(".countdown-text").TextContent.Should().Contain("left");
        cut.Find(".countdown-progress").GetAttribute("role").Should().Be("progressbar");
    }

    [Theory]
    [InlineData(35, "countdown-progress-green", 75.0, 100.0)]
    [InlineData(20, "countdown-progress-blue", 25.0, 75.0)]
    [InlineData(9, "countdown-progress-red", 0.0, 25.0)]
    public void QuestionCard_AppliesProgressThresholdClassAndAriaValues(int remainingSeconds, string expectedClass, double minInclusive, double maxInclusive)
    {
        var question = CreateTimedQuestion(40, remainingSeconds);

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => { })));

        var progressBar = cut.Find(".countdown-progress");
        var progressFill = cut.Find(".countdown-progress-fill");
        var progressValue = double.Parse(progressBar.GetAttribute("aria-valuenow")!, CultureInfo.InvariantCulture);

        progressFill.ClassList.Should().Contain(expectedClass);
        progressValue.Should().BeGreaterThanOrEqualTo(minInclusive);
        progressValue.Should().BeLessThanOrEqualTo(maxInclusive);
        progressFill.GetAttribute("style").Should().Contain("width:");
    }

    [Fact]
    public void QuestionCard_AdminAlsoSeesProgressBar_WhenTimingDataIsPresent()
    {
        var question = CreateTimedQuestion(40, 30);

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.IsAdmin, true));

        cut.Find(".countdown-progress").Should().NotBeNull();
        cut.Markup.Should().Contain("Players are answering");
    }

    [Fact]
    public void QuestionCard_AdminQuestionOpen_RendersProjectedCardsWithLabelsNotButtons()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, true),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false));

        var cut = Render<QuestionCard>(parameters => parameters
            .Add(component => component.Question, question)
            .Add(component => component.IsAdmin, true)
            .Add(component => component.OnAnswer, EventCallback.Factory.Create<int>(this, _ => throw new InvalidOperationException("Admin answers must not be clickable."))));

        var projectedCards = cut.FindAll(".projection-answer-card");

        projectedCards.Should().HaveCount(4);
        cut.FindAll("button.answer-button").Should().BeEmpty();
        projectedCards[0].TextContent.Should().Contain("A · Red");
        projectedCards[1].TextContent.Should().Contain("B · Blue");
        projectedCards[2].TextContent.Should().Contain("C · Green");
        projectedCards[3].TextContent.Should().Contain("D · Yellow");
        projectedCards[0].QuerySelector(".projection-answer-label")!.ClassList.Should().Contain("answer-label-badge");
    }

    [Fact]
    public void ResultCardRendersBarChartWithVoteCountsAndCorrectAnswer()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Red", 4, false),
            new AnswerOptionView(2, "Blue", 3, true),
            new AnswerOptionView(3, "Green", 2, false),
            new AnswerOptionView(4, "Yellow", 1, false));

        var cut = Render<ResultCard>(parameters => parameters
            .Add(component => component.Question, question));

        var bars = cut.FindAll(".result-bar-card");
        var fills = cut.FindAll(".result-bar-fill");

        bars.Should().HaveCount(4);
        fills.Should().HaveCount(4);
        fills[0].GetAttribute("style").Should().Contain("height: 40%;");
        fills[1].GetAttribute("style").Should().Contain("height: 30%;");
        fills[2].GetAttribute("style").Should().Contain("height: 20%;");
        fills[3].GetAttribute("style").Should().Contain("height: 10%;");
        bars[1].TextContent.Should().Contain("Correct");
        bars[1].ClassList.Should().Contain("correct-result");
        bars[0].TextContent.Should().Contain("4");
    }

    [Fact]
    public void ResultCardRendersQuestionAndAnswersAsHtmlMarkup()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Use <code>SRP</code>", 4, false),
            new AnswerOptionView(2, "<em>Blue</em>", 3, true),
            new AnswerOptionView(3, "Green", 2, false),
            new AnswerOptionView(4, "Yellow", 1, false)) with
        {
            Text = "What is <strong>SOLID</strong>?"
        };

        var cut = Render<ResultCard>(parameters => parameters
            .Add(component => component.Question, question));

        cut.Find("h2").InnerHtml.Should().Contain("<strong>SOLID</strong>");
        cut.Find(".result-bar-card.answer-1 strong").InnerHtml.Should().Contain("<code>SRP</code>");
        cut.Find(".result-bar-card.answer-2 strong").InnerHtml.Should().Contain("<em>Blue</em>");
        cut.Markup.Should().NotContain("&lt;strong&gt;SOLID&lt;/strong&gt;");
    }

    [Fact]
    public void ExplainCardRendersExplanationAsHtmlMarkup()
    {
        var question = CreateQuestion(
            new AnswerOptionView(1, "Red", 4, false),
            new AnswerOptionView(2, "Blue", 3, true),
            new AnswerOptionView(3, "Green", 2, false),
            new AnswerOptionView(4, "Yellow", 1, false)) with
        {
            Explanation = "<p>Because <code>SOLID</code> matters.</p>"
        };

        var cut = Render<ExplainCard>(parameters => parameters
            .Add(component => component.Question, question));

        var explanation = cut.Find(".explanation-text");

        explanation.InnerHtml.Should().Contain("<code>SOLID</code>");
        explanation.InnerHtml.Should().NotContain("&lt;code&gt;");
    }

    [Fact]
    public void Home_QuestionResults_ShowsResultLeaderboardAndExplanation()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var alice = CreateUser("Alice");
        ConfigureHomeServices(quizSession);
        AuthenticateAs(alice);

        quizSession.GetSnapshot(alice.UserName).Returns(CreateQuestionResultsSnapshot(alice));

        var cut = Render<Home>();

        cut.FindAll(".results-chart").Should().ContainSingle();
        cut.FindAll(".leaderboard-list").Should().ContainSingle();
        cut.FindAll(".winner-podium").Should().BeEmpty();
        cut.FindAll(".explanation-box").Should().ContainSingle();
        cut.Markup.Should().Contain("Question 1 results");
        cut.Markup.Should().Contain(alice.UserName);
    }

    [Fact]
    public void Home_Completed_ShowsPodiumAndLeaderboard()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var alice = CreateUser("Alice");
        ConfigureHomeServices(quizSession);
        AuthenticateAs(alice);

        quizSession.GetSnapshot(alice.UserName).Returns(CreateCompletedSnapshot(alice));

        var cut = Render<Home>();

        cut.FindAll(".winner-podium").Should().ContainSingle();
        cut.FindAll(".leaderboard-list").Should().ContainSingle();
        cut.Markup.Should().Contain("Champions podium");
        cut.FindAll(".results-chart").Should().BeEmpty();
        cut.FindAll(".explanation-box").Should().BeEmpty();
        cut.Markup.Should().NotContain("Una classe abstracta NO pot");
    }

    [Fact]
    public void SessionHeader_RendersRestartButton_ForAdminOnly()
    {
        var adminCut = Render<SessionHeader>(parameters => parameters
            .Add(component => component.UserName, "moderator")
            .Add(component => component.IsAdmin, true));
        var playerCut = Render<SessionHeader>(parameters => parameters
            .Add(component => component.UserName, "Alice")
            .Add(component => component.IsAdmin, false));

        adminCut.FindAll("button.danger-button").Should().ContainSingle();
        adminCut.Markup.Should().Contain("Restart quest");
        playerCut.FindAll("button.danger-button").Should().BeEmpty();
    }

    [Fact]
    public void WinnerPodium_RendersTopEntriesInOlympicOrder()
    {
        var cut = Render<WinnerPodium>(parameters => parameters
            .Add(component => component.Entries,
                [
                    new LeaderboardEntry("Ada", 8, 1200),
                    new LeaderboardEntry("Linus", 6, 1500),
                    new LeaderboardEntry("Grace", 4, 2200),
                    new LeaderboardEntry("Margaret", 2, 3000)
                ]));

        var podiumSlots = cut.FindAll(".podium-slot");

        podiumSlots.Should().HaveCount(3);
        podiumSlots[0].ClassList.Should().Contain("podium-rank-2");
        podiumSlots[0].TextContent.Should().Contain("Linus");
        podiumSlots[1].ClassList.Should().Contain("podium-rank-1");
        podiumSlots[1].TextContent.Should().Contain("Ada");
        podiumSlots[2].ClassList.Should().Contain("podium-rank-3");
        podiumSlots[2].TextContent.Should().Contain("Grace");
        cut.Markup.Should().NotContain("Margaret");
    }

    [Fact]
    public void WinnerPodium_HandlesFewerThanThreeEntries()
    {
        var cut = Render<WinnerPodium>(parameters => parameters
            .Add(component => component.Entries,
                [
                    new LeaderboardEntry("Ada", 8, 1200),
                    new LeaderboardEntry("Linus", 6, 1500)
                ]));

        var podiumSlots = cut.FindAll(".podium-slot");

        podiumSlots.Should().HaveCount(2);
        podiumSlots[0].ClassList.Should().Contain("podium-rank-2");
        podiumSlots[1].ClassList.Should().Contain("podium-rank-1");
        cut.Markup.Should().NotContain("podium-rank-3");
    }

    [Fact]
    public async Task Home_AdminLoad_ClickingLoadCallsTryLoadQuestAsync()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("moderator", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        quizSession.GetSnapshot(admin.UserName).Returns(CreateEnrollmentSnapshot(canStart: false, enrolledPlayers: ["Alice"]));
        quizSession
            .TryLoadQuestAsync(admin.UserName, string.Empty, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(QuizActionResult.Succeeded()));

        var cut = Render<Home>();

        cut.Find("button.primary-button").Click();

        await quizSession.Received(1).TryLoadQuestAsync(admin.UserName, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Home_AdminStart_ClickingStartCallsTryStartAsync()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("moderator", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        var acceptingPlayersSnapshot = new QuizSessionSnapshot(
            QuizStage.AcceptingPlayers,
            HasAdmin: true,
            CanStart: true,
            CanLoad: false,
            PlayerCount: 1,
            EnrolledPlayers: ["Alice"],
            QuestMetadata: new QuestMetadata("Test Quest", null, null),
            CurrentQuestion: null,
            Leaderboard: []);

        quizSession.GetSnapshot(admin.UserName).Returns(acceptingPlayersSnapshot);
        quizSession
            .TryStartAsync(admin.UserName, QuestionTimeoutSettings.DefaultSeconds, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(QuizActionResult.Succeeded()));

        var cut = Render<Home>();

        cut.Find("button.primary-button").Click();

        await quizSession.Received(1).TryStartAsync(admin.UserName, QuestionTimeoutSettings.DefaultSeconds, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Home_AdminLoad_WithQuestionsUrl_CallsTryLoadQuestAsyncWithUrl()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("moderator", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        quizSession.GetSnapshot(admin.UserName).Returns(CreateEnrollmentSnapshot(canStart: false, enrolledPlayers: ["Alice"]));
        quizSession
            .TryLoadQuestAsync(admin.UserName, "https://example.com/questions.yaml", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(QuizActionResult.Succeeded()));

        var cut = Render<Home>();

        cut.Find("#questions-url").Input("https://example.com/questions.yaml");
        cut.Find("button.primary-button").Click();

        await quizSession.Received(1).TryLoadQuestAsync(admin.UserName, "https://example.com/questions.yaml", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Home_AdminRestart_ClickingRestartCallsTryRestart()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("moderator", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        quizSession.GetSnapshot(admin.UserName).Returns(CreateCompletedSnapshot(admin));
        quizSession
            .TryRestart(admin.UserName, out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var cut = Render<Home>();

        cut.Find("button.danger-button").Click();

        quizSession.Received(1).TryRestart(admin.UserName, out Arg.Any<string>());
    }

    [Fact]
    public void EnrollmentLobby_NonAdmin_DoesNotRenderQuestionsUrlInput()
    {
        var cut = Render<EnrollmentLobby>(parameters => parameters
            .Add(component => component.Snapshot, CreateEnrollmentSnapshot(canStart: false, enrolledPlayers: ["Alice"]))
            .Add(component => component.IsAdmin, false));

        cut.FindAll("#questions-url").Should().BeEmpty();
        cut.FindAll("#question-timeout-seconds").Should().BeEmpty();
    }

    [Fact]
    public void LoginForm_RendersJoinPrompt()
    {
        Services.AddSingleton<IOptions<QuestOptions>>(Options.Create(new QuestOptions { AdminUserName = "moderator" }));

        var cut = Render<LoginForm>();

        cut.Markup.Should().Contain("Join the session");
        cut.Markup.Should().Contain("Pick a unique username.");
    }

    [Fact]
    public void Home_PlayerAnswer_ClickingAnswerCallsTrySubmitAnswer()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var alice = CreateUser("Alice");
        ConfigureHomeServices(quizSession);
        AuthenticateAs(alice);

        quizSession.GetSnapshot(alice.UserName).Returns(CreateQuestionOpenSnapshot());
        quizSession
            .TrySubmitAnswer(alice.UserName, 1, out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[2] = string.Empty;
                return true;
            });

        var cut = Render<Home>();

        cut.Find("button.answer-button.answer-1").Click();

        quizSession.Received(1).TrySubmitAnswer(alice.UserName, 1, out Arg.Any<string>());
    }

    [Fact]
    public void Home_AdminQuestionOpen_RendersProjectedCardsAndDoesNotSubmitAnswers()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("moderator", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        quizSession.GetSnapshot(admin.UserName).Returns(CreateQuestionOpenSnapshot());

        var cut = Render<Home>();

        cut.FindAll(".projection-answer-card").Should().HaveCount(4);
        cut.FindAll("button.answer-button").Should().BeEmpty();
        cut.FindAll(".top-bar-compact").Should().ContainSingle();
        cut.Markup.Should().Contain("A · Red");

        quizSession.DidNotReceive().TrySubmitAnswer(admin.UserName, Arg.Any<int>(), out Arg.Any<string>());
    }

    private static QuestionView CreateQuestion(params AnswerOptionView[] answers) => new(
        Number: 1,
        Total: 10,
        Text: "What is SOLID?",
        Image: null,
        ImageAlt: null,
        Answers: answers,
        Explanation: "Because design matters.",
        SelectedAnswer: null,
        CorrectAnswer: 2,
        Responses: answers.Sum(answer => answer.Votes),
        TotalPlayers: 10,
        TimeoutSeconds: null,
        DeadlineUtc: null,
        Points: 12);

    private static QuestionView CreateTimedQuestion(int timeoutSeconds, int remainingSeconds) => CreateQuestion(
        new AnswerOptionView(1, "Red", 0, false),
        new AnswerOptionView(2, "Blue", 0, true),
        new AnswerOptionView(3, "Green", 0, false),
        new AnswerOptionView(4, "Yellow", 0, false)) with
    {
        TimeoutSeconds = timeoutSeconds,
        DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(remainingSeconds)
    };

    private void ConfigureHomeServices(IQuizSessionService quizSession)
    {
        Services.AddSingleton<IOptions<QuestOptions>>(Options.Create(new QuestOptions()));
        Services.AddSingleton(quizSession);
        Services.AddSingleton<PlayerSession>();
        Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        Services.AddSingleton<CustomAuthStateProvider>();
    }

    private User CreateUser(string userName, bool isAdmin = false) => new()
    {
        UserName = userName,
        IsAdmin = isAdmin,
        RestoreToken = Guid.NewGuid().ToString()
    };

    private void AuthenticateAs(User user)
    {
        Services.GetRequiredService<PlayerSession>().Set(user.UserName, user.IsAdmin, user.RestoreToken);
    }

    private static QuizSessionSnapshot CreateEnrollmentSnapshot(bool canStart, IReadOnlyList<string> enrolledPlayers) => new(
        Stage: QuizStage.Enrollment,
        HasAdmin: true,
        CanStart: canStart,
        CanLoad: true,
        PlayerCount: enrolledPlayers.Count,
        EnrolledPlayers: enrolledPlayers,
        QuestMetadata: null,
        CurrentQuestion: null,
        Leaderboard: []);

    private static QuizSessionSnapshot CreateQuestionOpenSnapshot() => new(
        Stage: QuizStage.QuestionOpen,
        HasAdmin: true,
        CanStart: true,
        CanLoad: false,
        PlayerCount: 1,
        EnrolledPlayers: ["Alice"],
        QuestMetadata: new QuestMetadata("Test Quest", null, null),
        CurrentQuestion: CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, true),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)),
        Leaderboard: []);

    private static QuizSessionSnapshot CreateQuestionResultsSnapshot(User player) => new(
        Stage: QuizStage.QuestionResults,
        HasAdmin: true,
        CanStart: true,
        CanLoad: false,
        PlayerCount: 1,
        EnrolledPlayers: [player.UserName],
        QuestMetadata: new QuestMetadata("Test Quest", null, null),
        CurrentQuestion: CreateQuestion(
            new AnswerOptionView(1, "Red", 1, false),
            new AnswerOptionView(2, "Blue", 0, true),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            SelectedAnswer = 1,
            CorrectAnswer = 2,
            Responses = 1,
            TotalPlayers = 1
        },
        Leaderboard: [new LeaderboardEntry(player.UserName, 0, 0)]);

    private static QuizSessionSnapshot CreateCompletedSnapshot(User player) => new(
        Stage: QuizStage.Completed,
        HasAdmin: true,
        CanStart: true,
        CanLoad: false,
        PlayerCount: 1,
        EnrolledPlayers: [player.UserName],
        QuestMetadata: new QuestMetadata("Test Quest", null, null),
        CurrentQuestion: null,
        Leaderboard: [new LeaderboardEntry(player.UserName, 1, 0)]);

    [Fact]
    public async Task HomePageShowsPreviewModeWhenAdminClicksPreviewButton()
    {
        var quizService = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("admin", isAdmin: true);
        ConfigureHomeServices(quizService);
        AuthenticateAs(admin);
        
        var snapshot = new QuizSessionSnapshot(
            Stage: QuizStage.AcceptingPlayers,
            HasAdmin: true,
            CanStart: true,
            CanLoad: false,
            PlayerCount: 1,
            EnrolledPlayers: ["admin"],
            QuestMetadata: new QuestMetadata("Test Quest", null, null),
            CurrentQuestion: null,
            Leaderboard: []);
        
        quizService.GetSnapshot(admin.UserName).Returns(snapshot);
        quizService.GetQuestionCount().Returns(3);
        
        var previewQuestion = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, false),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            Number = 1,
            Total = 3,
            Points = 0,
            SelectedAnswer = null,
            CorrectAnswer = null,
            Explanation = null
        };
        
        quizService.GetPreviewQuestion(admin.UserName, 0).Returns(previewQuestion);
        
        var cut = Render<Home>();
        
        var previewButton = cut.Find("button:contains('Preview questions')");
        await previewButton.ClickAsync(new());
        
        cut.Markup.Should().Contain("Preview Mode");
        cut.Markup.Should().Contain("Exit Preview");
        cut.Markup.Should().Contain("Question 1 / 3");
    }

    [Fact]
    public async Task PreviewModeShowsQuestionWithoutCorrectAnswerOrExplanation()
    {
        var quizService = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("admin", isAdmin: true);
        ConfigureHomeServices(quizService);
        AuthenticateAs(admin);
        
        var snapshot = new QuizSessionSnapshot(
            Stage: QuizStage.AcceptingPlayers,
            HasAdmin: true,
            CanStart: true,
            CanLoad: false,
            PlayerCount: 1,
            EnrolledPlayers: ["admin"],
            QuestMetadata: new QuestMetadata("Test Quest", null, null),
            CurrentQuestion: null,
            Leaderboard: []);
        
        quizService.GetSnapshot(admin.UserName).Returns(snapshot);
        quizService.GetQuestionCount().Returns(2);
        
        var previewQuestion = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, false),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            Number = 1,
            Total = 2,
            Text = "What is your favorite color?",
            Points = 0,
            SelectedAnswer = null,
            CorrectAnswer = null,
            Explanation = null
        };
        
        quizService.GetPreviewQuestion(admin.UserName, 0).Returns(previewQuestion);
        
        var cut = Render<Home>();
        
        var previewButton = cut.Find("button:contains('Preview questions')");
        await previewButton.ClickAsync(new());
        
        cut.Markup.Should().Contain("What is your favorite color?");
        cut.FindAll(".projection-answer-card").Should().HaveCount(4);
        cut.Markup.Should().NotContain("points-badge");
        cut.Markup.Should().NotContain("countdown");
    }

    [Fact]
    public async Task PreviewModeNavigatesBetweenQuestions()
    {
        var quizService = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("admin", isAdmin: true);
        ConfigureHomeServices(quizService);
        AuthenticateAs(admin);
        
        var snapshot = new QuizSessionSnapshot(
            Stage: QuizStage.AcceptingPlayers,
            HasAdmin: true,
            CanStart: true,
            CanLoad: false,
            PlayerCount: 1,
            EnrolledPlayers: ["admin"],
            QuestMetadata: new QuestMetadata("Test Quest", null, null),
            CurrentQuestion: null,
            Leaderboard: []);
        
        quizService.GetSnapshot(admin.UserName).Returns(snapshot);
        quizService.GetQuestionCount().Returns(3);
        
        var question1 = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, false),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            Number = 1,
            Total = 3,
            Text = "Question 1",
            Points = 0
        };
        
        var question2 = CreateQuestion(
            new AnswerOptionView(1, "Cat", 0, false),
            new AnswerOptionView(2, "Dog", 0, false),
            new AnswerOptionView(3, "Bird", 0, false),
            new AnswerOptionView(4, "Fish", 0, false)) with
        {
            Number = 2,
            Total = 3,
            Text = "Question 2",
            Points = 0
        };
        
        quizService.GetPreviewQuestion(admin.UserName, 0).Returns(question1);
        quizService.GetPreviewQuestion(admin.UserName, 1).Returns(question2);
        
        var cut = Render<Home>();
        
        var previewButton = cut.Find("button:contains('Preview questions')");
        await previewButton.ClickAsync(new());
        
        cut.Markup.Should().Contain("Question 1");
        cut.Markup.Should().Contain("1 / 3");
        
        var nextButton = cut.Find("button:contains('Next')");
        await nextButton.ClickAsync(new());
        
        cut.Markup.Should().Contain("Question 2");
        cut.Markup.Should().Contain("2 / 3");
    }

    [Fact]
    public async Task PreviewModeExitsAndReturnsToLobby()
    {
        var quizService = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("admin", isAdmin: true);
        ConfigureHomeServices(quizService);
        AuthenticateAs(admin);
        
        var snapshot = new QuizSessionSnapshot(
            Stage: QuizStage.AcceptingPlayers,
            HasAdmin: true,
            CanStart: true,
            CanLoad: false,
            PlayerCount: 1,
            EnrolledPlayers: ["admin"],
            QuestMetadata: new QuestMetadata("Test Quest", null, null),
            CurrentQuestion: null,
            Leaderboard: []);
        
        quizService.GetSnapshot(admin.UserName).Returns(snapshot);
        quizService.GetQuestionCount().Returns(2);
        
        var previewQuestion = CreateQuestion(
            new AnswerOptionView(1, "Red", 0, false),
            new AnswerOptionView(2, "Blue", 0, false),
            new AnswerOptionView(3, "Green", 0, false),
            new AnswerOptionView(4, "Yellow", 0, false)) with
        {
            Number = 1,
            Total = 2,
            Points = 0
        };
        
        quizService.GetPreviewQuestion(admin.UserName, 0).Returns(previewQuestion);
        
        var cut = Render<Home>();
        
        var previewButton = cut.Find("button:contains('Preview questions')");
        await previewButton.ClickAsync(new());
        
        cut.Markup.Should().Contain("Preview Mode");
        
        var exitButton = cut.Find("button:contains('Exit Preview')");
        await exitButton.ClickAsync(new());
        
        cut.Markup.Should().NotContain("Preview Mode");
        cut.Markup.Should().Contain("Start session");
    }
}

