using Microsoft.AspNetCore.Http;

namespace QuestUI.Auth;

public static class QuestAuthCookie
{
    public const string CookieName = "solid-quest-user";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromHours(1);

    public static CookieOptions Create(HttpContext httpContext) => new()
    {
        Expires = DateTimeOffset.UtcNow.Add(CookieLifetime),
        HttpOnly = true,
        IsEssential = true,
        MaxAge = CookieLifetime,
        SameSite = SameSiteMode.Lax,
        Secure = httpContext.Request.IsHttps,
        Path = "/"
    };

    public static CookieOptions Delete(HttpContext httpContext) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = httpContext.Request.IsHttps,
        Path = "/"
    };
}
