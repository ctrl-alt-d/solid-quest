namespace QuestBackend;

public static class QuestionTimeoutSettings
{
    public const int DefaultSeconds = 30;
    public const int MinSeconds = 5;
    public const int MaxSeconds = 300;

    public static bool IsValid(int seconds) => seconds >= MinSeconds && seconds <= MaxSeconds;
}
