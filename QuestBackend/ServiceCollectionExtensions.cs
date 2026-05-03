using Microsoft.Extensions.DependencyInjection;

namespace QuestBackend;

public static class MyFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddMyFeature(this IServiceCollection services)
    {
        return services
            .AddSingleton<TimeProvider>(_ => TimeProvider.System)
            .AddSingleton<Users, Users>()
            .AddSingleton<QuestionLoader, QuestionLoader>()
            .AddSingleton<QuizSessionService, QuizSessionService>();
    }
}
