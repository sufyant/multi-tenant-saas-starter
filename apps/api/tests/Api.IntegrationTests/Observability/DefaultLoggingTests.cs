using ControlPlane.Application.Tenants.ActivateTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Wolverine.Util;

namespace Api.IntegrationTests.Observability;

// The default log settings of appsettings.json (smoke finding 12): a process logs each message it handled, by its name (W12), and EF Core
// does not log the SQL of every command.
public sealed class DefaultLoggingTests(Database database)
{
    [Fact]
    public async Task CreateTenant_WithTheDefaultLogSettings_LogsEachHandledMessageAndNoSql()
    {
        var admin = await new TestData(database).AddSystemAdminAsync();
        await using var api = new ApiFactory(database.ApplicationConnectionString, configureServices: services => services.AddFakeLogging());
        var client = api.CreateClient(admin, secondFactor: true);

        await api.WaitingForMessagesAsync(() =>
            client.CreateTenantAsync(new { name = "Acme Ltd", slug = $"tenant-{Guid.NewGuid():N}"[..20], ownerEmail = "owner@acme.test" }));

        var logs = api.Services.GetFakeLogCollector().GetSnapshot();
        logs.ShouldContain(record => record.Level == LogLevel.Information && record.Message.Contains(typeof(ActivateTenantCommand).ToMessageTypeName()));
        logs.ShouldNotContain(record => record.Category == "Microsoft.EntityFrameworkCore.Database.Command" && record.Level < LogLevel.Warning);
    }
}
