using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Infrastructure.Persistence;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace Api.IntegrationTests.Fixtures;

public sealed class IdempotencyCommitFailure
{
    public required string Key { get; init; }
    public bool SawTheSavedResult { get; private set; }

    public async Task AfterAsync(Envelope envelope, ControlPlaneDbContext controlPlane, CancellationToken cancellationToken)
    {
        if (envelope.Headers.GetValueOrDefault("idempotency-key") == Key)
        {
            SawTheSavedResult = await controlPlane.Database.SqlQuery<long>(
                $"SELECT count(*) AS \"Value\" FROM api.idempotency_keys WHERE key = {Key} AND result IS NOT NULL AND locked_until IS NULL").SingleAsync(cancellationToken) == 1;
            throw new InvalidOperationException("The commit failed after the result write.");
        }
    }
}

internal sealed class IdempotencyCommitFailurePolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(chain => chain.MessageType == typeof(CreateTenantCommand)))
        {
            chain.Postprocessors.Insert(1, new MethodCall(typeof(IdempotencyCommitFailure), nameof(IdempotencyCommitFailure.AfterAsync)));
        }
    }
}
