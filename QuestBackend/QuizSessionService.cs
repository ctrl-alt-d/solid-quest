using Microsoft.Extensions.Options;

namespace QuestBackend;

public sealed class QuizSessionService : IQuizSessionService
{
    private const int CorrectAnswerPoints = 1;

    private readonly IUsers _users;
    private readonly IQuestionLoader _questionLoader;
    private readonly TimeProvider _timeProvider;
    private readonly QuestOptions _questOptions;
    private readonly Lock _lock = new();

    private IReadOnlyList<Question> _questions = [];
    private Dictionary<string, int> _answers = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _questionTimer;
    private QuizStage _stage = QuizStage.Enrollment;
    private int _currentQuestionIndex = -1;
    private DateTimeOffset? _currentQuestionOpenedAt;
    private int _questionTimeoutSeconds = QuestionTimeoutSettings.DefaultSeconds;

    public QuizSessionService(IUsers users, IQuestionLoader questionLoader, TimeProvider timeProvider, IOptions<QuestOptions> questOptions)
    {
        _users = users;
        _questionLoader = questionLoader;
        _timeProvider = timeProvider;
        _questOptions = questOptions.Value;
    }

    public event Action? StateChanged;

    public QuizSessionSnapshot GetSnapshot(string? userName = null)
    {
        lock (_lock)
        {
            var enrolledPlayers = _users.GetPlayerNames();
            var leaderboard = _users.GetLeaderboard();

            return new QuizSessionSnapshot(
                _stage,
                _users.HasAdmin,
                _stage == QuizStage.Enrollment && enrolledPlayers.Count > 0,
                enrolledPlayers.Count,
                enrolledPlayers,
                BuildQuestionView(userName),
                leaderboard);
        }
    }

    public bool TryJoin(string userName, bool isAdmin, out string errorMessage)
        => TryJoin(userName, isAdmin, out _, out errorMessage);

    public bool TryJoin(string userName, bool isAdmin, out User? user, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            user = null;
            errorMessage = "Username is required.";
            return false;
        }

        var trimmedUserName = userName.Trim();

        lock (_lock)
        {
            if (!isAdmin && _stage != QuizStage.Enrollment)
            {
                user = null;
                errorMessage = "Enrollment is closed.";
                return false;
            }

            if (_questOptions.IsAdminUserName(trimmedUserName) && !isAdmin)
            {
                user = null;
                errorMessage = $"The username '{_questOptions.AdminUserName}' is reserved.";
                return false;
            }

            if (!_users.TryAdd(trimmedUserName, isAdmin, out user, out var duplicateMessage))
            {
                errorMessage = duplicateMessage!;
                return false;
            }
        }

        errorMessage = string.Empty;
        NotifyStateChanged();
        return true;
    }

    public bool TryRestoreUser(string restoreToken, out User? user)
    {
        if (string.IsNullOrWhiteSpace(restoreToken))
        {
            user = null;
            return false;
        }

        return _users.TryGetByRestoreToken(restoreToken, out user);
    }

    public void Leave(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        var normalizedUserName = Normalize(userName);
        var stateChanged = false;

        lock (_lock)
        {
            if (!_users.Contains(normalizedUserName))
            {
                return;
            }

            _users.Remove(normalizedUserName);
            _answers.Remove(normalizedUserName);

            if (_stage == QuizStage.QuestionOpen && ShouldRevealResults())
            {
                RevealResultsCore();
            }

            stateChanged = true;
        }

        if (stateChanged)
        {
            NotifyStateChanged();
        }
    }

    public void LeaveByRestoreToken(string restoreToken)
    {
        if (!TryRestoreUser(restoreToken, out var user) || user is null)
        {
            return;
        }

        Leave(user.UserName);
    }

    public async Task<QuizActionResult> TryStartAsync(string userName, string? questionsUrl, int questionTimeoutSeconds, CancellationToken cancellationToken = default)
    {
        if (!QuestionTimeoutSettings.IsValid(questionTimeoutSeconds))
        {
            return QuizActionResult.Failed($"Question timeout must be between {QuestionTimeoutSettings.MinSeconds} and {QuestionTimeoutSettings.MaxSeconds} seconds.");
        }

        lock (_lock)
        {
            if (!IsAdmin(userName))
            {
                return QuizActionResult.Failed("Only admin can start the session.");
            }

            if (_stage != QuizStage.Enrollment)
            {
                return QuizActionResult.Failed("The session has already started.");
            }

            if (_users.PlayerCount == 0)
            {
                return QuizActionResult.Failed("At least one player is required to start.");
            }
        }

        var questionLoadResult = string.IsNullOrWhiteSpace(questionsUrl)
            ? await _questionLoader.LoadSampleQuestionsAsync(cancellationToken)
            : await _questionLoader.LoadQuestionsFromUrlAsync(questionsUrl.Trim(), cancellationToken);

        if (!questionLoadResult.Success)
        {
            return QuizActionResult.Failed(questionLoadResult.ErrorMessage);
        }

        lock (_lock)
        {
            if (_stage != QuizStage.Enrollment)
            {
                return QuizActionResult.Failed("The session has already started.");
            }

            if (_users.PlayerCount == 0)
            {
                return QuizActionResult.Failed("At least one player is required to start.");
            }

            _questions = questionLoadResult.Questions;
            _questionTimeoutSeconds = questionTimeoutSeconds;
            _users.ResetScores();
            _currentQuestionIndex = 0;
            OpenQuestionCore();
        }

        NotifyStateChanged();
        return QuizActionResult.Succeeded();
    }

    public bool TryRestart(string userName, out string errorMessage)
    {
        lock (_lock)
        {
            if (!IsAdmin(userName))
            {
                errorMessage = "Only admin can restart the session.";
                return false;
            }

            ResetSessionCore();
        }

        NotifyStateChanged();
        errorMessage = string.Empty;
        return true;
    }

    public bool TrySubmitAnswer(string userName, int answerIndex, out string errorMessage)
    {
        lock (_lock)
        {
            if (_stage != QuizStage.QuestionOpen || _currentQuestionIndex < 0)
            {
                errorMessage = "There is no active question right now.";
                return false;
            }

            if (IsAdmin(userName))
            {
                errorMessage = "Admin cannot submit answers.";
                return false;
            }

            if (answerIndex < 1 || answerIndex > 4)
            {
                errorMessage = "Answer must be between 1 and 4.";
                return false;
            }

            var normalizedUserName = Normalize(userName);
            if (!_users.Contains(normalizedUserName))
            {
                errorMessage = "User is no longer enrolled.";
                return false;
            }

            if (_answers.ContainsKey(normalizedUserName))
            {
                errorMessage = "You already answered this question.";
                return false;
            }

            _answers[normalizedUserName] = answerIndex;
            _users.AddAnswerTime(normalizedUserName, GetElapsedMillisecondsForCurrentQuestion());

            if (ShouldRevealResults())
            {
                RevealResultsCore();
            }
        }

        NotifyStateChanged();
        errorMessage = string.Empty;
        return true;
    }

    public bool TryAdvance(string userName, out string errorMessage)
    {
        lock (_lock)
        {
            if (!IsAdmin(userName))
            {
                errorMessage = "Only admin can continue the session.";
                return false;
            }

            if (_stage != QuizStage.QuestionResults)
            {
                errorMessage = "The next question is not available yet.";
                return false;
            }

            if (_currentQuestionIndex >= _questions.Count - 1)
            {
                StopTimerCore();
                _stage = QuizStage.Completed;
            }
            else
            {
                _currentQuestionIndex++;
                OpenQuestionCore();
            }
        }

        NotifyStateChanged();
        errorMessage = string.Empty;
        return true;
    }

    private QuestionView? BuildQuestionView(string? userName)
    {
        if (_currentQuestionIndex < 0 || _currentQuestionIndex >= _questions.Count)
        {
            return null;
        }

        var question = _questions[_currentQuestionIndex];
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? null : Normalize(userName);
        int? selectedAnswer = normalizedUserName is null || !_answers.TryGetValue(normalizedUserName, out var answer)
            ? null
            : answer;
        var revealAnswers = _stage is QuizStage.QuestionResults or QuizStage.Completed;
        var totalPlayers = _users.PlayerCount;

        var answers = new List<AnswerOptionView>(4);
        for (var index = 1; index <= 4; index++)
        {
            answers.Add(new AnswerOptionView(
                index,
                GetAnswerText(question, index),
                _answers.Values.Count(value => value == index),
                revealAnswers && question.CorrectAnswer == index));
        }

        return new QuestionView(
            _currentQuestionIndex + 1,
            _questions.Count,
            question.Text,
            answers,
            revealAnswers ? question.Explanation : string.Empty,
            selectedAnswer,
            revealAnswers ? question.CorrectAnswer : null,
            _answers.Count,
            totalPlayers,
            _stage == QuizStage.QuestionOpen ? _questionTimeoutSeconds : null,
            GetCurrentQuestionDeadlineUtc());
    }

    private void ResetSessionCore()
    {
        StopTimerCore();
        _questions = [];
        _answers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _stage = QuizStage.Enrollment;
        _currentQuestionIndex = -1;
        _questionTimeoutSeconds = QuestionTimeoutSettings.DefaultSeconds;
        _users.ResetScores();
    }

    private void OpenQuestionCore()
    {
        StopTimerCore();
        _stage = QuizStage.QuestionOpen;
        _answers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _currentQuestionOpenedAt = _timeProvider.GetUtcNow();
        _questionTimer = new Timer(
            _ => RevealResultsFromTimer(),
            null,
            TimeSpan.FromSeconds(_questionTimeoutSeconds),
            Timeout.InfiniteTimeSpan);
    }

    private void RevealResultsFromTimer()
    {
        var stateChanged = false;

        lock (_lock)
        {
            if (_stage != QuizStage.QuestionOpen)
            {
                return;
            }

            RevealResultsCore();
            stateChanged = true;
        }

        if (stateChanged)
        {
            NotifyStateChanged();
        }
    }

    private void RevealResultsCore()
    {
        if (_stage != QuizStage.QuestionOpen)
        {
            return;
        }

        StopTimerCore();
        _stage = QuizStage.QuestionResults;

        var question = _questions[_currentQuestionIndex];
        foreach (var userName in _answers
                     .Where(entry => entry.Value == question.CorrectAnswer)
                     .Select(entry => entry.Key))
        {
            _users.AddScore(userName, CorrectAnswerPoints);
        }
    }

    private bool ShouldRevealResults()
    {
        var playerCount = _users.PlayerCount;
        return playerCount == 0 || _answers.Count >= playerCount;
    }

    private bool IsAdmin(string userName) => _questOptions.IsAdminUserName(Normalize(userName));

    private long GetElapsedMillisecondsForCurrentQuestion()
    {
        if (_currentQuestionOpenedAt is null)
        {
            return 0;
        }

        var elapsed = _timeProvider.GetUtcNow() - _currentQuestionOpenedAt.Value;
        return Math.Max(0L, (long)elapsed.TotalMilliseconds);
    }

    private DateTimeOffset? GetCurrentQuestionDeadlineUtc()
        => _stage == QuizStage.QuestionOpen && _currentQuestionOpenedAt is not null
            ? _currentQuestionOpenedAt.Value.AddSeconds(_questionTimeoutSeconds)
            : null;

    private void StopTimerCore()
    {
        _questionTimer?.Dispose();
        _questionTimer = null;
        _currentQuestionOpenedAt = null;
    }

    private static string GetAnswerText(Question question, int answerIndex) => answerIndex switch
    {
        1 => question.Answer1,
        2 => question.Answer2,
        3 => question.Answer3,
        4 => question.Answer4,
        _ => throw new ArgumentOutOfRangeException(nameof(answerIndex))
    };

    private static string Normalize(string userName) => userName.Trim();

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
