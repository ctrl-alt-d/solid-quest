using QuestBackend;

namespace QuestBackendTest;

public sealed class QuestionLoaderTests
{
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
    public void LoadQuestions_RendersCodeFencesAsPreformattedHtml()
    {
        var questions = new QuestionLoader().LoadQuestions();

        var explanation = questions[0].Explanation;

        Assert.Contains("<pre><code class=\"language-c#\">", explanation);
        Assert.Contains("public interface IUser", explanation);
        Assert.Contains("</code></pre>", explanation);
    }
}
