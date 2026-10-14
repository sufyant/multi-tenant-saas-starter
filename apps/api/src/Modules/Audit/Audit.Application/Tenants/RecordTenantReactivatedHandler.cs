using Audit.Application.Ports;
using ControlPlane.Contracts;

namespace Audit.Application.Tenants;

public static class RecordTenantReactivatedHandler
{
    public static Task HandleAsync(TenantReactivated reactivated, IAuditLog log, CancellationToken cancellationToken) => throw new NotImplementedException();
}
