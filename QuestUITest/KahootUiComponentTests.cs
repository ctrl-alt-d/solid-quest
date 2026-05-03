using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using QuestBackend;
using QuestUI.Auth;
using QuestUI.Components.Pages;
using QuestUI.Components.Quiz;

namespace QuestUITest;

public class KahootUiComponentTests : BunitContext
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
        cut.FindAll(".explanation-box").Should().ContainSingle();
        cut.Markup.Should().Contain("Question 1 results");
        cut.Markup.Should().Contain(alice.UserName);
    }

    [Fact]
    public void Home_Completed_ShowsOnlyLeaderboard()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var alice = CreateUser("Alice");
        ConfigureHomeServices(quizSession);
        AuthenticateAs(alice);

        quizSession.GetSnapshot(alice.UserName).Returns(CreateCompletedSnapshot(alice));

        var cut = Render<Home>();

        cut.FindAll(".leaderboard-list").Should().ContainSingle();
        cut.FindAll(".results-chart").Should().BeEmpty();
        cut.FindAll(".explanation-box").Should().BeEmpty();
        cut.Markup.Should().NotContain("Una classe abstracta NO pot");
    }

    [Fact]
    public void Home_AdminStart_ClickingStartCallsTryStart()
    {
        var quizSession = Substitute.For<IQuizSessionService>();
        var admin = CreateUser("admin", isAdmin: true);
        ConfigureHomeServices(quizSession);
        AuthenticateAs(admin);

        quizSession.GetSnapshot(admin.UserName).Returns(CreateEnrollmentSnapshot(canStart: true, enrolledPlayers: ["Alice"]));
        quizSession
            .TryStart(admin.UserName, out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var cut = Render<Home>();

        cut.Find("button.primary-button").Click();

        quizSession.Received(1).TryStart(admin.UserName, out Arg.Any<string>());
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

    private static QuestionView CreateQuestion(params AnswerOptionView[] answers) => new(
        Number: 1,
        Total: 10,
        Text: "What is SOLID?",
        Answers: answers,
        Explanation: "Because design matters.",
        SelectedAnswer: null,
        CorrectAnswer: 2,
        Responses: answers.Sum(answer => answer.Votes),
        TotalPlayers: 10);

    private void ConfigureHomeServices(IQuizSessionService quizSession)
    {
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
        PlayerCount: enrolledPlayers.Count,
        EnrolledPlayers: enrolledPlayers,
        CurrentQuestion: null,
        Leaderboard: []);

    private static QuizSessionSnapshot CreateQuestionOpenSnapshot() => new(
        Stage: QuizStage.QuestionOpen,
        HasAdmin: true,
        CanStart: true,
        PlayerCount: 1,
        EnrolledPlayers: ["Alice"],
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
        PlayerCount: 1,
        EnrolledPlayers: [player.UserName],
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
        PlayerCount: 1,
        EnrolledPlayers: [player.UserName],
        CurrentQuestion: null,
        Leaderboard: [new LeaderboardEntry(player.UserName, 1, 0)]);
}
