using Flashminds.Components;
using Flashminds.Data;
using Flashminds.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Entity Framework Core with SQLite
builder.Services.AddDbContextFactory<FlashmindsContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=flashminds.db"));

builder.Services.AddDbContext<AuthContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AuthConnection") ?? "Data Source=flashminds-auth.db"));

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/login");
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AuthContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Also add DbContext for direct injection if needed
builder.Services.AddScoped<FlashmindsContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<FlashmindsContext>>().CreateDbContextAsync().GetAwaiter().GetResult());

// Add application services
builder.Services.AddScoped<SpacedRepetitionService>();
builder.Services.AddScoped<DeckService>();

var app = builder.Build();

// Initialize database on startup
{
    var contextFactory = app.Services.GetRequiredService<IDbContextFactory<FlashmindsContext>>();
    var context = contextFactory.CreateDbContextAsync().GetAwaiter().GetResult();
    using (context)
    {
        context.Database.EnsureCreated();
    }

    using var scope = app.Services.CreateScope();
    var authContext = scope.ServiceProvider.GetRequiredService<AuthContext>();
    authContext.Database.EnsureCreated();
}
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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/login", async (HttpContext httpContext, IAntiforgery antiforgery, SignInManager<IdentityUser> signInManager) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var email = form["Email"].ToString();
    var password = form["Password"].ToString();
    var rememberMe = form["RememberMe"] == "on";

    var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
    return result.Succeeded
        ? Results.LocalRedirect(GetSafeReturnUrl(form["ReturnUrl"].ToString()))
        : Results.LocalRedirect("/login?error=invalid");
});

app.MapPost("/account/register", async (HttpContext httpContext, IAntiforgery antiforgery, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var confirmPassword = form["ConfirmPassword"].ToString();

    if (string.IsNullOrWhiteSpace(email) || password != confirmPassword)
    {
        return Results.LocalRedirect("/register?error=invalid");
    }

    var user = new IdentityUser { UserName = email, Email = email };
    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
        return Results.LocalRedirect("/register?error=invalid");
    }

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.LocalRedirect("/");
});

app.MapPost("/account/logout", async (HttpContext httpContext, IAntiforgery antiforgery, SignInManager<IdentityUser> signInManager) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/");
}).RequireAuthorization();

app.Run();

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
