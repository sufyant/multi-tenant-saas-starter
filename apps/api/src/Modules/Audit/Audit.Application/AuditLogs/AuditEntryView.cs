namespace Audit.Application.AuditLogs;

/// <summary>
/// One entry of an audit log as a reader sees it: the stored entry, with each person's name resolved now (K3). It holds no email
/// address. The kinds are the Domain's names (<c>User</c>, <c>Tenant</c>); the details are the stored JSON object, or null.
/// </summary>
public sealed record AuditEntryView(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Operation,
    string? Details,
    AuditActorView Actor,
    AuditTargetView Target);
