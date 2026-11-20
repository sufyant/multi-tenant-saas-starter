using ControlPlane.Application.Usage.UseMeter;
using ControlPlane.Contracts.Usage;
using Wolverine;

namespace ControlPlane.Infrastructure.Usage;

// The count runs as an inline command in the tenant (T2, W2), so Wolverine's transaction holds the row's lock only for this use (W1),
// whatever transaction the caller has open.
internal sealed class UsageMeter(IMessageBus bus) : IUsageMeter
{
    public Task<MeterUse> UseAsync(Guid tenantId, string meterKey, long quantity, CancellationToken cancellationToken) =>
        bus.InvokeForTenantAsync<MeterUse>(tenantId.ToString(), new UseMeterCommand(meterKey, quantity), cancellationToken);
}
