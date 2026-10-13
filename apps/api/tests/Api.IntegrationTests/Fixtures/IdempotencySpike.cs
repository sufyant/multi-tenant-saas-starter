using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

public sealed record SpikeCreateTenant(Guid Id, bool Fail);

public sealed record SpikeTenantResult(Guid Id);

public static class SpikeCreateTenantHandler
{
    public static async Task<Result<SpikeTenantResult>> Handle(SpikeCreateTenant command, ControlPlaneDbContext controlPlane, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync(
            $"INSERT INTO control_plane.tenants (id, name, slug, status) VALUES ({command.Id}, {"Spike"}, {$"spike-{command.Id:N}"}, {"Provisioning"})", cancellationToken);
        if (command.Fail)
        {
            throw new InvalidOperationException("The handler failed after the business write.");
        }

        return new SpikeTenantResult(command.Id);
    }
}

public static class SpikeResultMiddleware
{
    public static async Task AfterAsync(Result<SpikeTenantResult> result, Envelope envelope, ControlPlaneDbContext controlPlane, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync(
            $"INSERT INTO spike.idempotency_keys (key, result) VALUES ({envelope.Headers["spike-key"]}, {result.Value.Id.ToString()})", cancellationToken);
        if (envelope.Headers.ContainsKey("fail-after-result"))
        {
            throw new InvalidOperationException("The middleware failed after the result write.");
        }
    }
}
