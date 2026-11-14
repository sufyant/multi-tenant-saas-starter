using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.TermsVersions;

namespace Audit.Application.TermsVersions.RecordTermsVersionUpdated;

// The audit entry of an edit of the scheduled terms version, in the platform's log: the values before and after.
public static class RecordTermsVersionUpdatedHandler
{
    public static Task HandleAsync(TermsVersionUpdatedEvent updated, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new TermsVersionUpdatedDetails(Of(updated.Old), Of(updated.New)), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(
                updated.EventId, updated.OccurredAt, AuditActor.User(updated.UpdatedBy), AuditOperations.TermsVersionUpdated, AuditTarget.TermsVersion(updated.VersionId), details),
            null,
            cancellationToken);
    }

    private static AuditTermsVersion Of(TermsVersionRecord version) => new(version.EffectiveDate, version.Url, version.Summary, version.RequiresAcceptance, version.SendsEmail);
}
