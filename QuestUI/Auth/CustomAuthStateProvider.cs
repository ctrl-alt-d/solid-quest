using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using QuestBackend;

namespace QuestUI.Auth;

public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly PlayerSession _playerSession;
    private readonly IQuizSessionService _quizSession;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool _restoreAttempted;

    public CustomAuthStateProvider(PlayerSession playerSession, IQuizSessionService quizSession, IHttpContextAccessor httpContextAccessor)
    {
        _playerSession = playerSession;
        _quizSession = quizSession;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        RestoreFromCookieIfNeeded();
        return Task.FromResult(CreateAuthenticationState());
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

    private void RestoreFromCookieIfNeeded()
    {
        if (_playerSession.IsAuthenticated || _restoreAttempted)
        {
            return;
        }

        _restoreAttempted = true;

        var restoreToken = _httpContextAccessor.HttpContext?.Request.Cookies[QuestAuthCookie.CookieName];
        if (string.IsNullOrWhiteSpace(restoreToken))
        {
            return;
        }

        if (_quizSession.TryRestoreUser(restoreToken, out var user) && user is not null)
        {
            _playerSession.Set(user.UserName, user.IsAdmin, user.RestoreToken);
        }
    }
}
