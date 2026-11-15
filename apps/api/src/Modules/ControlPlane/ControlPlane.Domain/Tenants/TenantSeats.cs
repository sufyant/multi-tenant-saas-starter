namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a tenant's summary keeps of its seats (<see cref="TenantSummary.Summarize"/>): how many members have a role that takes no seat,
/// and when each pending invitation expires, all of them and those of a role that takes no seat (<c>Role.TakesSeat</c>).
/// </summary>
internal sealed record TenantSeats(int FreeMembers, IReadOnlyList<DateTimeOffset> PendingInvitationExpiries, IReadOnlyList<DateTimeOffset> FreeInvitationExpiries);
