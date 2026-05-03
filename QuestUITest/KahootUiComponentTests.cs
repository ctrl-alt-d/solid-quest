using System.Linq;
using Microsoft.AspNetCore.Components;
using QuestBackend;
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

        Assert.Collection(buttons,
            button => Assert.Contains("answer-1", button.ClassList),
            button => Assert.Contains("answer-2", button.ClassList),
            button => Assert.Contains("answer-3", button.ClassList),
            button => Assert.Contains("answer-4", button.ClassList));
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

        Assert.Equal(4, bars.Count);
        Assert.Equal(4, fills.Count);
        Assert.Contains("height: 40%;", fills[0].GetAttribute("style"));
        Assert.Contains("height: 30%;", fills[1].GetAttribute("style"));
        Assert.Contains("height: 20%;", fills[2].GetAttribute("style"));
        Assert.Contains("height: 10%;", fills[3].GetAttribute("style"));
        Assert.Contains("Correct", bars[1].TextContent);
        Assert.Contains("correct-result", bars[1].ClassList);
        Assert.Contains("4", bars[0].TextContent);
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

        Assert.Contains("<code>SOLID</code>", explanation.InnerHtml);
        Assert.DoesNotContain("&lt;code&gt;", explanation.InnerHtml);
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
}
