namespace QuestBackend;

public interface IQuestionLoader
{
    Task<QuestionLoadResult> LoadSampleQuestionsAsync(CancellationToken cancellationToken = default);
    Task<QuestionLoadResult> LoadQuestionsFromUrlAsync(string url, CancellationToken cancellationToken = default);
}
