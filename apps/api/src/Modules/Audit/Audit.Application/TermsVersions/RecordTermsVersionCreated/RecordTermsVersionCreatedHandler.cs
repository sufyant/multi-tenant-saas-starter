using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.TermsVersions;

namespace Audit.Application.TermsVersions.RecordTermsVersionCreated;

// The audit entry of a new terms version, in the platform's log: a version belongs to no tenant. The system admin who created it acted
// (K3); the details hold its values.
public static class RecordTermsVersionCreatedHandler
{
    public static Task HandleAsync(TermsVersionCreatedEvent created, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new TermsVersionCreatedDetails(created.EffectiveDate, created.Url, created.Summary, created.RequiresAcceptance), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(
                created.EventId, created.OccurredAt, AuditActor.User(created.CreatedBy), AuditOperations.TermsVersionCreated, AuditTarget.TermsVersion(created.VersionId), details),
            null,
            cancellationToken);
    }
}
