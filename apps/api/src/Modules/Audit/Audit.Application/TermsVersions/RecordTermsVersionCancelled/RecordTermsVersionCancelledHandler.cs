using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.TermsVersions;

namespace Audit.Application.TermsVersions.RecordTermsVersionCancelled;

// The audit entry of a cancelled terms version, in the platform's log, with how many users had accepted it: their acceptances stay.
public static class RecordTermsVersionCancelledHandler
{
    public static Task HandleAsync(TermsVersionCancelledEvent cancelled, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new TermsVersionCancelledDetails(cancelled.EffectiveDate, cancelled.Url, cancelled.RequiresAcceptance, cancelled.AcceptanceCount), JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(
                cancelled.EventId,
                cancelled.OccurredAt,
                AuditActor.User(cancelled.CancelledBy),
                AuditOperations.TermsVersionCancelled,
                AuditTarget.TermsVersion(cancelled.VersionId),
                details),
            null,
            cancellationToken);
    }
}
