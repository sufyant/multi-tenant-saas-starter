using Audit.Application.Ports;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

public static class RecordTenantSuspendedHandler
{
    public static Task HandleAsync(TenantSuspended suspended, IAuditLog log, CancellationToken cancellationToken) => throw new NotImplementedException();
}
