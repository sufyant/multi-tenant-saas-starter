namespace Audit.Domain.AuditEntries;

/// <summary>What the action was done to. A person carries no name (K3).</summary>
internal sealed record AuditTarget(AuditTargetKind Kind, Guid Id, string? Name)
{
    public static AuditTarget Tenant(Guid tenantId, string? name) => new(AuditTargetKind.Tenant, tenantId, name);

    public static AuditTarget User(Guid userId) => new(AuditTargetKind.User, userId, null);

    /// <summary>An invitation, by its id only: its email address is a person's (K3).</summary>
    public static AuditTarget Invitation(Guid invitationId) => new(AuditTargetKind.Invitation, invitationId, null);

    /// <summary>A role of the tenant, with its name of that moment: a role is not a person (K3).</summary>
    public static AuditTarget Role(Guid roleId, string name) => new(AuditTargetKind.Role, roleId, name);
}
