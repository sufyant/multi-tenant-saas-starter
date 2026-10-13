using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Idempotency;

public sealed class IdempotencySpikeTests(Database database)
{
    [Fact]
    public async Task SaveResult_BusinessWriteFails_RollsBackBothAndCommitsBothOnSuccess()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        await database.ScalarAsync<object>(
            "CREATE SCHEMA api; CREATE TABLE api.idempotency_keys (key text PRIMARY KEY, result text NOT NULL); GRANT USAGE ON SCHEMA api TO api_application; GRANT SELECT, INSERT ON api.idempotency_keys TO api_application;",
            DatabaseRoles.Owner, name);
        await using var host = await PipelineHost.StartAsync(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = database.ConnectionStringFor(DatabaseRoles.Application, name) },
            services => services.ConfigureWolverine(options => options.Policies.AddMiddleware(typeof(SpikeResultMiddleware),
                chain => chain.MessageType == typeof(SpikeCreateTenant))));
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var failed = Guid.CreateVersion7();
        var failedAfterResult = Guid.CreateVersion7();
        var succeeded = Guid.CreateVersion7();

        await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeAsync<Result<SpikeTenantResult>>(
            new SpikeCreateTenant(failed, true), new DeliveryOptions { TenantId = failed.ToString(), Headers = { ["idempotency-key"] = failed.ToString() } }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(() => bus.InvokeAsync<Result<SpikeTenantResult>>(
            new SpikeCreateTenant(failedAfterResult, false), new DeliveryOptions { TenantId = failedAfterResult.ToString(), Headers = { ["idempotency-key"] = failedAfterResult.ToString(), ["fail-after-result"] = "true" } }, TestContext.Current.CancellationToken));
        var result = await bus.InvokeAsync<Result<SpikeTenantResult>>(
            new SpikeCreateTenant(succeeded, false), new DeliveryOptions { TenantId = succeeded.ToString(), Headers = { ["idempotency-key"] = succeeded.ToString() } }, TestContext.Current.CancellationToken);

        (await database.ScalarAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE id = '{failed}'", database: name)).ShouldBe(0);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{failed}'", database: name)).ShouldBe(0);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE id = '{succeeded}'", database: name)).ShouldBe(1);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE id = '{failedAfterResult}'", database: name)).ShouldBe(0);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{failedAfterResult}'", database: name)).ShouldBe(0);
        (await database.ScalarAsync<string>($"SELECT result FROM api.idempotency_keys WHERE key = '{succeeded}'", database: name)).ShouldBe(result.Value.Id.ToString());
    }
}
