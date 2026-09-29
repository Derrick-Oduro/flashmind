using System.Security.Claims;
using Flashminds.Components;
using Flashminds.Contracts;
using Flashminds.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The web app keeps no data of its own. Signing in exchanges credentials for an API token,
// which is stored inside the auth cookie and forwarded on every API call.
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "flashminds.auth";
        options.LoginPath = "/login";
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<AccessTokenProvider>();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl must point at the FlashMind API.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// A page that loads data with a rejected API token (expired, or the API key changed) would
// otherwise end in a 500. Drop the stale cookie and send the user back to sign in instead.
app.Use(async (httpContext, next) =>
{
    try
    {
        await next();
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized && !httpContext.Response.HasStarted)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        httpContext.Response.Redirect("/login?error=expired");
    }
});

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Form endpoints. Each one validates the antiforgery token, calls the API, then redirects.
app.MapPost("/decks/create", async (HttpContext httpContext, IAntiforgery antiforgery, ApiClient api) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var title = form["Title"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(title))
        return Results.LocalRedirect("/decks/new");

    try
    {
        await api.CreateDeckAsync(new DeckRequest { Title = title, Description = form["Description"].ToString().Trim() });
    }
    catch (ApiException)
    {
        return Results.LocalRedirect("/decks/new");
    }

    return Results.LocalRedirect("/decks");
}).RequireAuthorization();

app.MapPost("/decks/{deckId:int}/delete", async (int deckId, HttpContext httpContext, IAntiforgery antiforgery, ApiClient api) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    try
    {
        await api.DeleteDeckAsync(deckId);
    }
    catch (ApiException)
    {
        // Already gone or not the user's deck: the list below shows the current state either way.
    }

    return Results.LocalRedirect("/decks");
}).RequireAuthorization();

app.MapPost("/deck/{deckId:int}/cards/create", async (int deckId, HttpContext httpContext, IAntiforgery antiforgery, ApiClient api) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var question = form["Question"].ToString().Trim();
    var answer = form["Answer"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        return Results.LocalRedirect($"/deck/{deckId}/cards/new");

    try
    {
        await api.CreateCardAsync(deckId, new CardRequest
        {
            Question = question,
            Answer = answer,
            Hint = form["Hint"].ToString(),
            Explanation = form["Explanation"].ToString()
        });
    }
    catch (ApiException)
    {
        return Results.LocalRedirect($"/deck/{deckId}/cards/new");
    }

    return Results.LocalRedirect($"/deck/{deckId}/edit");
}).RequireAuthorization();

app.MapPost("/account/login", async (HttpContext httpContext, IAntiforgery antiforgery, ApiClient api) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    try
    {
        var auth = await api.LoginAsync(new LoginRequest { Email = form["Email"].ToString(), Password = form["Password"].ToString() });
        await SignInAsync(httpContext, auth, isPersistent: form["RememberMe"] == "on");
    }
    catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
    {
        return Results.LocalRedirect("/login?error=unavailable");
    }
    catch (ApiException)
    {
        return Results.LocalRedirect("/login?error=invalid");
    }

    return Results.LocalRedirect(GetSafeReturnUrl(form["ReturnUrl"].ToString()));
});

app.MapPost("/account/register", async (HttpContext httpContext, IAntiforgery antiforgery, ApiClient api) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    try
    {
        var auth = await api.RegisterAsync(new RegisterRequest
        {
            Email = form["Email"].ToString().Trim(),
            Password = form["Password"].ToString(),
            ConfirmPassword = form["ConfirmPassword"].ToString()
        });
        await SignInAsync(httpContext, auth, isPersistent: false);
    }
    catch (ApiException ex)
    {
        return Results.LocalRedirect($"/register?error=invalid&message={Uri.EscapeDataString(ex.Message)}");
    }

    return Results.LocalRedirect("/");
});

app.MapPost("/account/logout", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
}).RequireAuthorization();

// The API rejected our token (expired or invalid): drop the cookie and ask the user to sign in again.
app.MapGet("/account/expired", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login?error=expired");
});

app.Run();

static async Task SignInAsync(HttpContext httpContext, AuthResponse auth, bool isPersistent)
{
    var identity = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, auth.UserId),
        new Claim(ClaimTypes.Name, auth.Email),
        new Claim(ClaimTypes.Email, auth.Email),
        new Claim(AccessTokenProvider.ClaimType, auth.Token)
    ], CookieAuthenticationDefaults.AuthenticationScheme);

    // The cookie can never outlive the API token it carries.
    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = isPersistent, ExpiresUtc = auth.ExpiresAt, AllowRefresh = false });
}

static string GetSafeReturnUrl(string? returnUrl) =>
    !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
        ? returnUrl
        : "/";

static async Task<bool> IsAntiforgeryValidAsync(HttpContext httpContext, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}
