using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.Tenants;

namespace Audit.Application.Plans.RecordContractStarted;

// A system admin marked the tenant Contract (B12, 12.12, 7.11), written when Billing announces it, under that tenant. The platform's log
// names the system admin, and the tenant's own log says only that the platform acted (K3).
public static class RecordContractStartedHandler
{
    public static Task HandleAsync(ContractStartedEvent started, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(started.TenantId, started.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(started.EventId, started.OccurredAt, AuditActor.User(started.StartedBy), AuditOperations.TenantContractStarted, tenant),
            new TenantAuditEntry(started.EventId, started.OccurredAt, AuditActor.Platform, AuditOperations.TenantContractStarted, tenant),
            cancellationToken);
    }
}
