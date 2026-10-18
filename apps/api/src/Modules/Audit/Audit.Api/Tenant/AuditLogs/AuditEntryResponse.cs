namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// One entry of an audit log: when, the operation (<c>member.invited</c>), who acted, what it was done to, and the details the entry
/// keeps: the shape of its operation, or null for an operation without details. Names are resolved when the log is read; a name is only
/// a person's real name. It holds no email address.
/// </summary>
public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Operation,
    AuditDetailsResponse? Details,
    AuditActorResponse Actor,
    AuditTargetResponse Target);
