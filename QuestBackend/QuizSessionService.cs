namespace QuestBackend;

public sealed class QuizSessionService
{
    private const int CorrectAnswerPoints = 1;
    private static readonly TimeSpan QuestionDuration = TimeSpan.FromSeconds(30);

    private readonly Users _users;
    private readonly IReadOnlyList<Question> _questions;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();

    private Dictionary<string, int> _answers = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _questionTimer;
    private QuizStage _stage = QuizStage.Enrollment;
    private int _currentQuestionIndex = -1;
    private DateTimeOffset? _currentQuestionOpenedAt;

    public QuizSessionService(Users users, QuestionLoader questionLoader, TimeProvider timeProvider)
    {
        _users = users;
        _questions = questionLoader.LoadQuestions();
        _timeProvider = timeProvider;
        ValidateQuestions(_questions);
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
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            errorMessage = "Username is required.";
            return false;
        }

        var trimmedUserName = userName.Trim();

        lock (_lock)
        {
            if (!isAdmin && _stage != QuizStage.Enrollment)
            {
                errorMessage = "Enrollment is closed.";
                return false;
            }

            if (trimmedUserName.Equals("admin", StringComparison.OrdinalIgnoreCase) && !isAdmin)
            {
                errorMessage = "The username 'admin' is reserved.";
                return false;
            }

            if (!_users.TryAdd(trimmedUserName, isAdmin, out var duplicateMessage))
            {
                errorMessage = duplicateMessage!;
                return false;
            }
        }

        errorMessage = string.Empty;
        NotifyStateChanged();
        return true;
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

    public bool TryStart(string userName, out string errorMessage)
    {
        lock (_lock)
        {
            if (!IsAdmin(userName))
            {
                errorMessage = "Only admin can start the session.";
                return false;
            }

            if (_stage != QuizStage.Enrollment)
            {
                errorMessage = "The session has already started.";
                return false;
            }

            if (_users.PlayerCount == 0)
            {
                errorMessage = "At least one player is required to start.";
                return false;
            }

            _users.ResetScores();
            _currentQuestionIndex = 0;
            OpenQuestionCore();
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
            totalPlayers);
    }

    private void OpenQuestionCore()
    {
        StopTimerCore();
        _stage = QuizStage.QuestionOpen;
        _answers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _currentQuestionOpenedAt = _timeProvider.GetUtcNow();
        _questionTimer = new Timer(_ => RevealResultsFromTimer(), null, QuestionDuration, Timeout.InfiniteTimeSpan);
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

    private bool IsAdmin(string userName) => Normalize(userName).Equals("admin", StringComparison.Ordinal);

    private long GetElapsedMillisecondsForCurrentQuestion()
    {
        if (_currentQuestionOpenedAt is null)
        {
            return 0;
        }

        var elapsed = _timeProvider.GetUtcNow() - _currentQuestionOpenedAt.Value;
        return Math.Max(0L, (long)elapsed.TotalMilliseconds);
    }

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

    private static void ValidateQuestions(IReadOnlyList<Question> questions)
    {
        if (questions.Count == 0)
        {
            throw new InvalidOperationException("At least one question is required.");
        }

        if (questions.Any(question => question.CorrectAnswer is < 1 or > 4))
        {
            throw new InvalidOperationException("Each question must define a correct answer between 1 and 4.");
        }

        if (questions.Any(question => new[] { question.Answer1, question.Answer2, question.Answer3, question.Answer4 }
                .Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidOperationException("Each question must define exactly four answers.");
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
