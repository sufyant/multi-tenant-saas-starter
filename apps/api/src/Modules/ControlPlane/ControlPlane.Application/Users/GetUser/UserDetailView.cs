namespace ControlPlane.Application.Users.GetUser;

/// <summary>
/// A user as in the list, with when a system admin suspended them and that system admin's names, and every membership by the tenant's
/// name, then id.
/// </summary>
public sealed record UserDetailView(
    UserView User,
    DateTimeOffset? SuspendedAt,
    UserNameView? SuspendedBy,
    IReadOnlyList<UserMembershipView> Memberships);
