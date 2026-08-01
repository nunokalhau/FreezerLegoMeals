using Domain.DotNet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Repository.DotNet;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

public sealed class SalsaVerdeSeedLocalizationIntegrationTests
{
    [Fact]
    public async Task GeneratedSeed_Preserves_CanonicalRecipeId_For_SalsaVerde_LocalizedRetrieval()
    {
        var workspaceRoot = ResolveWorkspaceRoot();
        var schemaPath = Path.Combine(workspaceRoot, "data", "recipes.sqlite.sql");
        var manualSeedPath = Path.Combine(workspaceRoot, "data", "recipes_manual_seed.sql");
        var localizedSeedPath = Path.Combine(workspaceRoot, "data", "food", "proteins", "salsa_verde_chicken.localization.seed.sql");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await ExecuteScriptAsync(connection, schemaPath);
        await ExecuteScriptAsync(connection, manualSeedPath);
        await ExecuteScriptAsync(connection, localizedSeedPath);

        var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new FreezerLegoMealsContext(options);
        var queryService = new LocalizedRecipeQueryService(context);

        var canonical = await context.Recipes.AsNoTracking().SingleAsync(recipe => recipe.Id == 2);
        Assert.Equal("Salsa Verde Chicken", canonical.Name);

        var localized = await queryService.GetLocalizedRecipeByIdAsync(2, LocalizationOptions.Create("pt", strictMode: true));

        Assert.NotNull(localized);
        Assert.Equal(2, localized!.CanonicalRecipeId);
        Assert.Equal("pt", localized.Language);
        Assert.Equal("Frango Salsa Verde", localized.Name);
        Assert.Null(localized.FallbackLanguageUsed);
    }

    private static async Task ExecuteScriptAsync(SqliteConnection connection, string scriptPath)
    {
        var sql = await File.ReadAllTextAsync(scriptPath);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string ResolveWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "package.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Workspace root could not be resolved from integration test base directory.");
    }
}
