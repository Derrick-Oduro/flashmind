using Flashminds.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Api.Data;

/// <summary>
/// Applies EF Core migrations at startup. Also baselines and patches databases that were created
/// before migrations existed so that existing deployments keep working.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FlashmindsContext>();
        await MigrateOrBaselineSqliteAsync(context);
        await EnsureDeckOwnerColumnAsync(context);
        await EnsureCardCreationColumnsAsync(context);

        var authContext = scope.ServiceProvider.GetRequiredService<AuthContext>();
        await MigrateOrBaselineSqliteAsync(authContext);

        // Optionally assign decks created before accounts existed to a known user.
        var legacyOwnerEmail = configuration["LegacyDeckOwnerEmail"];
        if (!string.IsNullOrWhiteSpace(legacyOwnerEmail))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var legacyOwner = await userManager.FindByEmailAsync(legacyOwnerEmail);
            if (legacyOwner is not null)
            {
                await context.Decks
                    .Where(deck => deck.OwnerId == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(deck => deck.OwnerId, legacyOwner.Id));
            }
        }
    }

    private static async Task MigrateOrBaselineSqliteAsync(DbContext context)
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

    private static async Task EnsureDeckOwnerColumnAsync(FlashmindsContext context)
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

    private static async Task EnsureCardCreationColumnsAsync(FlashmindsContext context)
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
}
