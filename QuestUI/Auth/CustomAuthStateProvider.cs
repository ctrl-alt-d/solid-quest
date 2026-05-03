using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using QuestBackend;

namespace QuestUI.Auth;

public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly PlayerSession _playerSession;
    private readonly QuizSessionService _quizSession;

    public CustomAuthStateProvider(PlayerSession playerSession, QuizSessionService quizSession)
    {
        _playerSession = playerSession;
        _quizSession = quizSession;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(CreateAuthenticationState());

    public Task<LoginResult> LoginAsync(string userName)
    {
        if (_playerSession.IsAuthenticated)
        {
            return Task.FromResult(LoginResult.Failure("A user is already logged in for this session."));
        }

        var trimmedUserName = userName?.Trim() ?? string.Empty;
        var isAdmin = trimmedUserName == "admin";

        if (!_quizSession.TryJoin(trimmedUserName, isAdmin, out var errorMessage))
        {
            return Task.FromResult(LoginResult.Failure(errorMessage));
        }

        _playerSession.Set(trimmedUserName, isAdmin);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.FromResult(LoginResult.Success());
    }

    public Task LogoutAsync()
    {
        if (_playerSession.IsAuthenticated)
        {
            _quizSession.Leave(_playerSession.UserName);
            _playerSession.Clear();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        return Task.CompletedTask;
    }

    private AuthenticationState CreateAuthenticationState()
    {
        if (!_playerSession.IsAuthenticated)
        {
            return AnonymousState;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _playerSession.UserName)
        };

        if (_playerSession.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, nameof(CustomAuthStateProvider));
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
