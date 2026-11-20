using ControlPlane.Application.Tenants.GetPaysByCard;
using ControlPlane.Application.Usage.UseMeter;
using ControlPlane.Contracts.Usage;
using Wolverine;

namespace ControlPlane.Infrastructure.Usage;

// The count runs as an inline command in the tenant (T2, W2), so Wolverine's transaction holds the row's lock only for this use (W1).
// Billing answers first whether the tenant pays by card, in a query of its own, as a handler gets the transaction of one module's DbContext
// (W1): only such a tenant has a spending cap (E9).
internal sealed class UsageMeter(IMessageBus bus) : IUsageMeter
{
    public async Task<MeterUse> UseAsync(Guid tenantId, string meterKey, long quantity, CancellationToken cancellationToken)
    {
        var paysByCard = await bus.InvokeAsync<bool>(new GetPaysByCardQuery(tenantId), cancellationToken);
        return await bus.InvokeForTenantAsync<MeterUse>(tenantId.ToString(), new UseMeterCommand(meterKey, quantity, paysByCard), cancellationToken);
    }
}
