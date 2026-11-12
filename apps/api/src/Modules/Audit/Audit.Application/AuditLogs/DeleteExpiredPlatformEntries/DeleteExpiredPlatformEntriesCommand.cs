using SharedKernel;

namespace Audit.Application.AuditLogs.DeleteExpiredPlatformEntries;

/// <summary>Deletes a batch of the platform's entries older than <c>Audit:Retention:Days</c> (K15).</summary>
[MessageName("delete-expired-platform-entries")]
public sealed record DeleteExpiredPlatformEntriesCommand;
