namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What an acceptance needs to know of the person who accepts (<see cref="Tenant.Accept"/>), read after the user's lock (K6f): whether
/// the user is deleted, whether they are a system admin, and whether they are a member of the tenant already. A person who is not a user
/// yet is none of these. The facts span two aggregates, the user and the tenant (M2), so the handler reads them through both
/// repositories (<see cref="None"/> for a person who is not a user yet).
/// </summary>
internal sealed record AcceptanceFacts(bool UserDeleted, bool IsSystemAdmin, bool IsMember)
{
    public static readonly AcceptanceFacts None = new(UserDeleted: false, IsSystemAdmin: false, IsMember: false);
}
