namespace ControlPlane.Application.Members;

/// <summary>
/// A member: the catalog user, the catalog's copy of their profile (A7), the role they hold in the tenant, whether the
/// identity provider deleted the user, and when they joined the tenant. The profile is empty when it did (A8).
/// </summary>
public sealed record MemberView(Guid UserId, string? Email, string? FirstName, string? LastName, RoleView Role, bool Deleted, DateTimeOffset JoinedAt);
