using QuestBackend;

namespace QuestBackendTest;

public sealed class QuestionLoaderTests
{
    [Fact]
    public void LoadQuestions_ConvertsMarkdownQuestionTextAndAnswersToHtml()
    {
        var questions = new QuestionLoader().LoadQuestions();

        var firstQuestion = questions[0];
        var secondQuestion = questions[1];

        Assert.Equal("Una <strong>interfície</strong>", firstQuestion.Text);
        Assert.Contains("<strong>mètodes</strong>", firstQuestion.Answer1);
        Assert.Contains("<code>new()</code>", firstQuestion.Answer2);
        Assert.Equal("Una classe abstracta <strong>NO</strong> pot", secondQuestion.Text);
        Assert.Contains("<code>new()</code>", secondQuestion.Answer1);
    }

    [Fact]
    public void LoadQuestions_ConvertsMarkdownExplanationToHtml()
    {
        var questions = new QuestionLoader().LoadQuestions();

        var explanation = questions[0].Explanation;

        Assert.Contains("<p>En C#, una interfície defineix un contracte.</p>", explanation);
        Assert.Contains("<ul>", explanation);
        Assert.DoesNotContain("```c#", explanation);
    }

    [Fact]
    public void LoadQuestions_DoesNotRequireTypeFieldInYaml()
    {
        var questions = new QuestionLoader().LoadQuestions();

        Assert.Equal(2, questions.Count);
    }

    [Fact]
    public void LoadQuestions_RendersCodeFencesAsPreformattedHtml()
    {
        var questions = new QuestionLoader().LoadQuestions();

        var explanation = questions[0].Explanation;

        Assert.Contains("<pre><code class=\"language-c#\">", explanation);
        Assert.Contains("public interface IUser", explanation);
        Assert.Contains("</code></pre>", explanation);
    }
}
