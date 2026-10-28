namespace ControlPlane.Domain.Plans;

/// <summary>
/// What a tenant has of what the keys count: its custom roles, its members and its pending invitations that have not expired. Each key's
/// count is written here once (D1), so a preview, the tenant's plan and the tenant web read the same number.
/// </summary>
internal sealed record EntitlementUsage(int CustomRoles, int Members, int PendingInvitations)
{
    /// <summary>
    /// The count of the key: what a limit counts, and, for a feature, what the tenant has that uses it. A feature is in use while the
    /// count is more than 0.
    /// </summary>
    public int CountOf(string key) => key switch
    {
        FeatureKeys.RolesCustom or LimitKeys.RolesCustomMax => CustomRoles,
        LimitKeys.MembersMax => Members + PendingInvitations,
        _ => throw new ArgumentException("The key is not on the code's list.", nameof(key)),
    };
}
