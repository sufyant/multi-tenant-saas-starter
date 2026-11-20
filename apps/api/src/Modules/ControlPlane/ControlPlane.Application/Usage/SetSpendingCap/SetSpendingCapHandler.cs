using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.SpendingCaps;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Usage.SetSpendingCap;

// The member's command locks the active tenant first (K6g), so a use, which holds the tenant row FOR SHARE, waits for it and then finds the
// new cap (K6q). Rejections come first (W7). The cap it has already changes nothing and records nothing (I1). The tenant's Activity log
// records the change by the member (12.12).
public static class SetSpendingCapHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        SetSpendingCapCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        ISpendingCapRepository caps,
        BillingOptions billing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (command.Amount is { } amount && SpendingCap.Check(amount) is { IsSuccess: false } invalid)
        {
            return (invalid, []);
        }

        var locked = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return (locked.Error, []);
        }

        var cap = await caps.FindForUpdateAsync(cancellationToken);
        var oldAmount = cap?.Amount;
        var change = (cap, command.Amount) switch
        {
            (null, null) => StateChange.Unchanged,
            (null, { } added) => Add(caps, new SpendingCap(added)),
            ({ } kept, null) => Remove(caps, kept),
            ({ } kept, { } changed) => kept.Change(changed),
        };
        if (change == StateChange.Unchanged)
        {
            return (Result.Success(), []);
        }

        await caps.SaveChangesAsync(cancellationToken);
        var (tenant, actor, _) = locked.Value;
        var now = time.GetUtcNow();
        return (Result.Success(), [new SpendingCapChangedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor.Id, oldAmount, command.Amount, billing.Currency)]);
    }

    private static StateChange Add(ISpendingCapRepository caps, SpendingCap cap)
    {
        caps.Add(cap);
        return StateChange.Changed;
    }

    private static StateChange Remove(ISpendingCapRepository caps, SpendingCap cap)
    {
        caps.Remove(cap);
        return StateChange.Changed;
    }
}
