using Microsoft.Extensions.DependencyInjection;

namespace QuestBackend;

public static class MyFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddMyFeature(this IServiceCollection services)
    {
        return services
            .AddSingleton<Users, Users>()
            .AddSingleton<QuestionLoader, QuestionLoader>()
            .AddSingleton<QuizSessionService, QuizSessionService>();
    }
}
