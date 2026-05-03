using Microsoft.Extensions.DependencyInjection;

namespace QuestBackend;

public static class MyFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddMyFeature(this IServiceCollection services)
    {
        return services
            .AddSingleton<TimeProvider>(_ => TimeProvider.System)
            .AddSingleton<IUsers, Users>()
            .AddSingleton<IQuestionLoader, QuestionLoader>()
            .AddSingleton<IQuizSessionService, QuizSessionService>();
    }
}
