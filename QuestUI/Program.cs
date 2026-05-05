using Microsoft.AspNetCore.Components.Authorization;
using QuestBackend;
using QuestUI.Auth;
using QuestUI.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddOptions<QuestOptions>()
    .BindConfiguration(QuestOptions.SectionName);
builder.Services.AddMyFeature();
builder.Services.AddScoped<PlayerSession>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(serviceProvider => serviceProvider.GetRequiredService<CustomAuthStateProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();
app.Use(async (httpContext, next) =>
{
    if (HttpMethods.IsGet(httpContext.Request.Method)
        && !Path.HasExtension(httpContext.Request.Path.Value)
        && httpContext.Request.Cookies.TryGetValue(QuestAuthCookie.CookieName, out var restoreToken)
        && !string.IsNullOrWhiteSpace(restoreToken))
    {
        var quizSession = httpContext.RequestServices.GetRequiredService<IQuizSessionService>();
        if (!quizSession.TryRestoreUser(restoreToken, out _))
        {
            httpContext.Response.Cookies.Delete(QuestAuthCookie.CookieName, QuestAuthCookie.Delete(httpContext));
        }
    }

    await next();
});

app.MapStaticAssets();
app.MapQuestAuthEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
