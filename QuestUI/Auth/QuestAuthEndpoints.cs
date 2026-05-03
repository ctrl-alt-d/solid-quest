using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using QuestBackend;

namespace QuestUI.Auth;

public static class QuestAuthEndpoints
{
    public static IEndpointRouteBuilder MapQuestAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", async Task<IResult> (HttpContext httpContext, IQuizSessionService quizSession) =>
        {
            var form = await httpContext.Request.ReadFormAsync();
            var userName = form["username"].ToString();
            var isAdmin = string.Equals(userName.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

            if (!quizSession.TryJoin(userName, isAdmin, out var user, out var errorMessage) || user is null)
            {
                httpContext.Response.Cookies.Delete(QuestAuthCookie.CookieName, QuestAuthCookie.Delete(httpContext));

                var errorUrl = QueryHelpers.AddQueryString("/", new Dictionary<string, string?>
                {
                    ["loginError"] = errorMessage,
                    ["username"] = userName
                });

                return Results.Redirect(errorUrl);
            }

            httpContext.Response.Cookies.Append(QuestAuthCookie.CookieName, user.RestoreToken, QuestAuthCookie.Create(httpContext));
            return Results.Redirect("/");
        }).DisableAntiforgery();

        endpoints.MapPost("/auth/logout", (HttpContext httpContext, IQuizSessionService quizSession) =>
        {
            var restoreToken = httpContext.Request.Cookies[QuestAuthCookie.CookieName];
            if (!string.IsNullOrWhiteSpace(restoreToken))
            {
                quizSession.LeaveByRestoreToken(restoreToken);
            }

            httpContext.Response.Cookies.Delete(QuestAuthCookie.CookieName, QuestAuthCookie.Delete(httpContext));
            return Results.Redirect("/");
        }).DisableAntiforgery();

        return endpoints;
    }
}
