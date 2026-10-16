using Api.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 9, rule 6: a host on the shared database runs no idempotency cleanup.
public sealed class SharedDatabaseCleanupTests(Database database)
{
    [Fact]
    public async Task StartApiFactory_OnTheSharedDatabase_SchedulesNoIdempotencyCleanup()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        api.Services.GetServices<IHostedService>().ShouldNotContain(service => service is IdempotencyCleanup);
    }

    [Fact]
    public async Task StartPipelineHost_OnTheSharedDatabase_SchedulesNoIdempotencyCleanup()
    {
        await using var host = await PipelineHost.StartAsync();

        host.Services.GetServices<IHostedService>().ShouldNotContain(service => service is IdempotencyCleanup);
    }

    [Fact]
    public async Task StartApiFactory_OnADatabaseOfItsOwn_SchedulesTheIdempotencyCleanup()
    {
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, await database.CreateMigratedDatabaseAsync()));

        api.Services.GetServices<IHostedService>().ShouldContain(service => service is IdempotencyCleanup);
    }
}
