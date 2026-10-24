namespace ControlPlane.Domain.Roles;

/// <summary>
/// What uses a role of the tenant: its memberships, and its pending invitations, an expired one included, as only a finished invitation
/// gives the role to nobody. A role in use cannot be deleted (role.in_use), and the role screens show both counts.
/// </summary>
internal sealed record RoleUse(int Members, int PendingInvitations)
{
    /// <summary>A new role: nothing uses it yet.</summary>
    public static RoleUse None { get; } = new(0, 0);

    public bool InUse => Members > 0 || PendingInvitations > 0;
}
