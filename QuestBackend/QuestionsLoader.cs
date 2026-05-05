using System.Net;
using Markdig;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace QuestBackend;

public class QuestionLoader : IQuestionLoader
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly string[] BlockHtmlTags = ["<p", "<pre", "<ul", "<ol", "<blockquote", "<h1", "<h2", "<h3", "<h4", "<h5", "<h6", "<table", "<hr"];

    private readonly HttpClient _httpClient;

    public QuestionLoader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<QuestionLoadResult> LoadSampleQuestionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadFromYaml(Questions));

    public async Task<QuestionLoadResult> LoadQuestionsFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return QuestionLoadResult.Failed("Enter a valid absolute HTTP or HTTPS URL.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DownloadTimeout);

        try
        {
            using var response = await _httpClient.GetAsync(uri, timeoutCts.Token);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return QuestionLoadResult.Failed($"The question URL returned {(int)response.StatusCode} {response.ReasonPhrase}. Expected HTTP 200 OK.");
            }

            var yaml = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            return LoadFromYaml(yaml);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return QuestionLoadResult.Failed("Timed out while downloading questions from the URL.");
        }
        catch (HttpRequestException)
        {
            return QuestionLoadResult.Failed("Could not download questions from the URL. Check the address and try again.");
        }
    }

    private static QuestionLoadResult LoadFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        QuestionsYaml? payload;

        try
        {
            payload = deserializer.Deserialize<QuestionsYaml>(yaml);
        }
        catch (YamlException)
        {
            return QuestionLoadResult.Failed("Questions YAML could not be parsed.");
        }

        if (payload?.Questions is null)
        {
            return QuestionLoadResult.Failed("Questions YAML must define a questions list.");
        }

        var validationError = ValidateQuestions(payload.Questions);
        if (validationError is not null)
        {
            return QuestionLoadResult.Failed(validationError);
        }

        var questions = payload.Questions.Select(q => new Question
        {
            Text = ConvertInlineMarkdownToHtml(q.Title),
            Answer1 = ConvertInlineMarkdownToHtml(q.Options[0]),
            Answer2 = ConvertInlineMarkdownToHtml(q.Options[1]),
            Answer3 = ConvertInlineMarkdownToHtml(q.Options[2]),
            Answer4 = ConvertInlineMarkdownToHtml(q.Options[3]),
            CorrectAnswer = q.CorrectAnswer,
            Explanation = ConvertMarkdownToHtml(q.Explanation),
        }).ToList();

        return QuestionLoadResult.Succeeded(questions);
    }

    private static string ConvertMarkdownToHtml(string markdown) => Markdown.ToHtml(markdown, MarkdownPipeline).Trim();

    private static string ConvertInlineMarkdownToHtml(string markdown)
    {
        var html = ConvertMarkdownToHtml(markdown);

        if (!html.StartsWith("<p>", StringComparison.Ordinal) || !html.EndsWith("</p>", StringComparison.Ordinal))
        {
            return html;
        }

        var innerHtml = html[3..^4].Trim();

        return BlockHtmlTags.Any(tag => innerHtml.Contains(tag, StringComparison.OrdinalIgnoreCase))
            ? html
            : innerHtml;
    }

    private static string? ValidateQuestions(IReadOnlyList<QuestionYaml> questions)
    {
        if (questions.Count == 0)
        {
            return "At least one question is required.";
        }

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            var questionNumber = index + 1;

            if (string.IsNullOrWhiteSpace(question.Title))
            {
                return $"Question {questionNumber} must define non-empty text.";
            }

            if (question.Options is null || question.Options.Count != 4)
            {
                return $"Question {questionNumber} must define exactly 4 options.";
            }

            if (question.Options.Any(string.IsNullOrWhiteSpace))
            {
                return $"Question {questionNumber} must define non-empty options.";
            }

            if (question.CorrectAnswer is < 1 or > 4)
            {
                return $"Question {questionNumber} must define a correct answer between 1 and 4.";
            }

            if (string.IsNullOrWhiteSpace(question.Explanation))
            {
                return $"Question {questionNumber} must define a non-empty explanation.";
            }
        }

        return null;
    }

    private const string Questions = """
    title: "Alguns conceptes bàsics"
    questions:
      - title: "Una **interfície**"
        options:
          - "És un contracte que defineix **mètodes** i propietats a implementar"
          - "Es pot instanciar amb `new()`"
          - "No pot implementar altres interfícies"
          - ".NET no té interfícies"
        correct_answer: 1
        explanation: |
          En C#, una interfície defineix un contracte.

          ```c#
          public interface IUser
          {
              void SetPassword(string password);
              int Edat { get; set; }
          }
          ```

          - No es pot instanciar directament.
          - Sí que pot heretar d'altres interfícies.

      - title: "Una classe abstracta **NO** pot"
        options:
          - "Instanciar-se amb `new()`"
          - "Heretar d'altres classes"
          - "Implementar interfícies"
          - "Tenir mètodes abstractes i implementats"
        correct_answer: 1
        explanation: |
          Una classe abstracta no es pot instanciar directament.

          ```c#
          public abstract class UserBase : IUser
          {
              public abstract void SetPassword(string password);
              public int Edat { get; set; }
          }
          ```

          - Pot heretar d'una altra classe.
          - Pot implementar interfícies.
          - Pot tenir mètodes abstractes i implementats.
    """;

    private class QuestionsYaml
    {
        public string? Title { get; set; }
        public required List<QuestionYaml> Questions { get; set; }
    }

    private class QuestionYaml
    {
        public required string Title { get; set; }
        public required List<string> Options { get; set; }
        public required int CorrectAnswer { get; set; }
        public required string Explanation { get; set; }
    }
}
