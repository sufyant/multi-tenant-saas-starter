using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.Tenants;

namespace Audit.Application.Plans.RecordContractEnded;

// A system admin ended the tenant's Contract (B12, 12.12, 7.11), written when Billing announces it, under that tenant. The platform's log
// names the system admin, and the tenant's own log says only that the platform acted (K3).
public static class RecordContractEndedHandler
{
    public static Task HandleAsync(ContractEndedEvent ended, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(ended.TenantId, ended.TenantName);
        return log.RecordAsync(
            new PlatformAuditEntry(ended.EventId, ended.OccurredAt, AuditActor.User(ended.EndedBy), AuditOperations.TenantContractEnded, tenant),
            new TenantAuditEntry(ended.EventId, ended.OccurredAt, AuditActor.Platform, AuditOperations.TenantContractEnded, tenant),
            cancellationToken);
    }
}
