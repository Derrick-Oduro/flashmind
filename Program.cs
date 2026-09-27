using System.Security.Claims;
using Flashminds.Components;
using Flashminds.Data;
using Flashminds.Models;
using Flashminds.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

app.UseForwardedHeaders();

// Initialize database on startup
{
    var contextFactory = app.Services.GetRequiredService<IDbContextFactory<FlashmindsContext>>();
    var context = contextFactory.CreateDbContextAsync().GetAwaiter().GetResult();
    using (context)
    {
        await MigrateOrBaselineSqliteAsync(context);
        await EnsureDeckOwnerColumnAsync(context);
        await EnsureCardCreationColumnsAsync(context);
    }

    using var scope = app.Services.CreateScope();
    var authContext = scope.ServiceProvider.GetRequiredService<AuthContext>();
    await MigrateOrBaselineSqliteAsync(authContext);

    var legacyOwnerEmail = app.Configuration["LegacyDeckOwnerEmail"];
    if (!string.IsNullOrWhiteSpace(legacyOwnerEmail))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var legacyOwner = await userManager.FindByEmailAsync(legacyOwnerEmail);
        if (legacyOwner is not null)
        {
            await using var studyContext = await contextFactory.CreateDbContextAsync();
            await studyContext.Decks
                .Where(deck => deck.OwnerId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(deck => deck.OwnerId, legacyOwner.Id));
        }
    }
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/decks/create", async (HttpContext httpContext, IAntiforgery antiforgery, IDbContextFactory<FlashmindsContext> contextFactory) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var title = form["Title"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(title))
        return Results.LocalRedirect("/decks/new");

    await using var context = await contextFactory.CreateDbContextAsync();
    context.Decks.Add(new Deck
    {
        Title = title,
        Description = form["Description"].ToString().Trim(),
        OwnerId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });
    await context.SaveChangesAsync();
    return Results.LocalRedirect("/decks");
}).RequireAuthorization();

app.MapPost("/decks/{deckId:int}/delete", async (int deckId, HttpContext httpContext, IAntiforgery antiforgery, IDbContextFactory<FlashmindsContext> contextFactory) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    await using var context = await contextFactory.CreateDbContextAsync();
    var deck = await context.Decks.FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);
    if (deck is not null)
    {
        context.Decks.Remove(deck);
        await context.SaveChangesAsync();
    }

    return Results.LocalRedirect("/decks");
}).RequireAuthorization();

app.MapPost("/deck/{deckId:int}/cards/create", async (int deckId, HttpContext httpContext, IAntiforgery antiforgery, IDbContextFactory<FlashmindsContext> contextFactory) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
        return Results.BadRequest();

    var form = await httpContext.Request.ReadFormAsync();
    var question = form["Question"].ToString().Trim();
    var answer = form["Answer"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        return Results.LocalRedirect($"/deck/{deckId}/cards/new");

    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    await using var context = await contextFactory.CreateDbContextAsync();
    var ownsDeck = await context.Decks.AnyAsync(item => item.Id == deckId && item.OwnerId == userId);
    if (ownsDeck)
    {
        context.Cards.Add(new Card
        {
            DeckId = deckId,
            Question = question,
            Answer = answer,
            Hint = string.IsNullOrWhiteSpace(form["Hint"]) ? null : form["Hint"].ToString().Trim(),
            Explanation = string.IsNullOrWhiteSpace(form["Explanation"]) ? null : form["Explanation"].ToString().Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    return Results.LocalRedirect($"/deck/{deckId}/edit");
}).RequireAuthorization();

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

static async Task MigrateOrBaselineSqliteAsync(DbContext context)
{
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        await using var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        var hasTables = Convert.ToInt32(await tablesCommand.ExecuteScalarAsync()) > 0;

        await using var historyCommand = connection.CreateCommand();
        historyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
        var hasMigrationHistory = Convert.ToInt32(await historyCommand.ExecuteScalarAsync()) > 0;

        if (!hasTables || hasMigrationHistory)
        {
            await connection.CloseAsync();
            await context.Database.MigrateAsync();
            return;
        }
    }
    finally
    {
        if (connection.State == System.Data.ConnectionState.Open)
            await connection.CloseAsync();
    }

    await context.Database.EnsureCreatedAsync();
    var initialMigration = context.Database.GetMigrations().First();
    var productVersion = context.Model.FindAnnotation("ProductVersion")?.Value?.ToString() ?? "8.0.11";

    await connection.OpenAsync();
    try
    {
        await using var createHistoryCommand = connection.CreateCommand();
        createHistoryCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            )
            """;
        await createHistoryCommand.ExecuteNonQueryAsync();

        await using var insertHistoryCommand = connection.CreateCommand();
        insertHistoryCommand.CommandText = "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion)";
        var migrationParameter = insertHistoryCommand.CreateParameter();
        migrationParameter.ParameterName = "$migrationId";
        migrationParameter.Value = initialMigration;
        insertHistoryCommand.Parameters.Add(migrationParameter);
        var versionParameter = insertHistoryCommand.CreateParameter();
        versionParameter.ParameterName = "$productVersion";
        versionParameter.Value = productVersion;
        insertHistoryCommand.Parameters.Add(versionParameter);
        await insertHistoryCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task EnsureDeckOwnerColumnAsync(FlashmindsContext context)
{
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        var ownerColumnExists = false;
        {
            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "PRAGMA table_info(\"Decks\")";
            await using var reader = await columnsCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader["name"]?.ToString(), "OwnerId", StringComparison.OrdinalIgnoreCase))
                {
                    ownerColumnExists = true;
                    break;
                }
            }
        }

        if (!ownerColumnExists)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE \"Decks\" ADD COLUMN \"OwnerId\" TEXT NULL";
            await alterCommand.ExecuteNonQueryAsync();
        }

        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_Decks_OwnerId\" ON \"Decks\" (\"OwnerId\")";
        await indexCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task EnsureCardCreationColumnsAsync(FlashmindsContext context)
{
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var columnsCommand = connection.CreateCommand())
        {
            columnsCommand.CommandText = "PRAGMA table_info(\"Cards\")";
            await using var reader = await columnsCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader["name"]?.ToString() ?? string.Empty);
            }
        }

        var missingColumns = new Dictionary<string, string>
        {
            ["CardType"] = "TEXT NOT NULL DEFAULT 'Basic'",
            ["Hint"] = "TEXT NULL",
            ["Explanation"] = "TEXT NULL"
        };

        foreach (var (columnName, columnDefinition) in missingColumns.Where(column => !existingColumns.Contains(column.Key)))
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE \"Cards\" ADD COLUMN \"{columnName}\" {columnDefinition}";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
}
