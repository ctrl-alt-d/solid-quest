using System.Net;
using System.Text;
using QuestBackend;

namespace QuestBackendTest;

public sealed class QuestionLoaderTests
{
    [Fact]
    public async Task LoadSampleQuestionsAsync_ConvertsMarkdownQuestionTextAndAnswersToHtml()
    {
        var result = await CreateLoader().LoadSampleQuestionsAsync();
        var questions = result.Questions;

        var firstQuestion = questions[0];
        var secondQuestion = questions[1];

        Assert.True(result.Success);
        Assert.Contains("Quí es aquest personatge?", firstQuestion.Text);
        Assert.Equal("https://i.imgur.com/3Wjd4JG.jpeg", firstQuestion.Image);
        Assert.Equal("Gat amb cos de torrada que vola", firstQuestion.ImageAlt);
        Assert.Contains("Nyan Cat", firstQuestion.Answer1);
        Assert.Equal("Six seven", secondQuestion.Text);
        Assert.Contains("Encongir-se d", secondQuestion.Answer1);
        Assert.Null(secondQuestion.Image);
    }

    [Fact]
    public async Task LoadSampleQuestionsAsync_ConvertsMarkdownExplanationToHtml()
    {
        var result = await CreateLoader().LoadSampleQuestionsAsync();
        var explanation = result.Questions[0].Explanation;

        Assert.True(result.Success);
        Assert.Contains("<p>Nyan Cat és un meme d'internet que va sorgir el 2011.</p>", explanation);
        Assert.Contains("video a yotube", explanation);
        Assert.DoesNotContain("```", explanation);
    }

    [Fact]
    public async Task LoadSampleQuestionsAsync_DoesNotRequireTypeFieldInYaml()
    {
        var result = await CreateLoader().LoadSampleQuestionsAsync();

        Assert.True(result.Success);
        Assert.Equal(2, result.Questions.Count);
    }

    [Fact]
    public async Task LoadSampleQuestionsAsync_RendersCodeFencesAsPreformattedHtml()
    {
        var result = await CreateLoader().LoadSampleQuestionsAsync();
        var explanation = result.Questions[1].Explanation; // Second question has code fence

        Assert.True(result.Success);
        Assert.Contains("<pre><code class=\"language-c#\">", explanation);
        Assert.Contains("public abstract class UserBase", explanation);
        Assert.Contains("</code></pre>", explanation);
    }

    [Fact]
    public async Task LoadQuestionsFromUrlAsync_LoadsQuestions_WhenHttp200ReturnsYaml()
    {
        var yaml = """
            title: "Remote Quest"
            questions:
              - title: "Remote question"
                options:
                  - "One"
                  - "Two"
                  - "Three"
                  - "Four"
                correct_answer: 2
                explanation: "Because **two**."
            """;

        var loader = CreateLoader(HttpStatusCode.OK, yaml);

        var result = await loader.LoadQuestionsFromUrlAsync("https://example.com/questions.yaml");

        Assert.True(result.Success);
        Assert.Single(result.Questions);
        Assert.Equal("Remote question", result.Questions[0].Text);
        Assert.Contains("<strong>two</strong>", result.Questions[0].Explanation);
    }

    [Fact]
    public async Task LoadQuestionsFromUrlAsync_ReturnsFriendlyError_WhenUrlIsInvalid()
    {
        var result = await CreateLoader().LoadQuestionsFromUrlAsync("not-a-url");

        Assert.False(result.Success);
        Assert.Equal("Enter a valid absolute HTTP or HTTPS URL.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadQuestionsFromUrlAsync_ReturnsFriendlyError_WhenHttpStatusIsNot200()
    {
        var loader = CreateLoader(HttpStatusCode.NotFound, "questions: []");

        var result = await loader.LoadQuestionsFromUrlAsync("https://example.com/questions.yaml");

        Assert.False(result.Success);
        Assert.Contains("Expected HTTP 200 OK", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadQuestionsFromUrlAsync_ReturnsFriendlyError_WhenYamlIsInvalid()
    {
        var loader = CreateLoader(HttpStatusCode.OK, "questions: [");

        var result = await loader.LoadQuestionsFromUrlAsync("https://example.com/questions.yaml");

        Assert.False(result.Success);
        Assert.Equal("Questions YAML could not be parsed.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadQuestionsFromUrlAsync_ReturnsFriendlyError_WhenQuestionsAreInvalid()
    {
        var yaml = """
            title: "Invalid Quest"
            questions:
              - title: ""
                options:
                  - "One"
                  - "Two"
                  - "Three"
                  - "Four"
                correct_answer: 2
                explanation: "Because."
            """;

        var loader = CreateLoader(HttpStatusCode.OK, yaml);

        var result = await loader.LoadQuestionsFromUrlAsync("https://example.com/questions.yaml");

        Assert.False(result.Success);
        Assert.Equal("Question 1 must define non-empty text.", result.ErrorMessage);
    }

    private static QuestionLoader CreateLoader(HttpStatusCode statusCode = HttpStatusCode.OK, string responseBody = "title: Sample\nquestions: []")
        => new(new HttpClient(new StubHttpMessageHandler(statusCode, responseBody)));

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/yaml")
            });
    }
}
