using Api.Persistence;

namespace Api.IntegrationTests;

// Runs the migration step the way a deployment does, as `dotnet Api.dll migrate`.
internal static class MigrateCommand
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string migrationsConnectionString)
    {
        var result = await RunWithinTheTimeLimitAsync(migrationsConnectionString);
        return (result.ExitCode, result.Output);
    }

    public static Task<ApiProcessResult> RunWithinTheTimeLimitAsync(string migrationsConnectionString) =>
        ApiProcess.RunAsync(
            new Dictionary<string, string> { ["ConnectionStrings__Migrations"] = migrationsConnectionString },
            MigrationStep.Command);
}
