namespace ControlPlane.Application.Users.GetUser;

/// <summary>
/// A user as in the list, with when a system admin suspended them, that system admin's names and the reason, the names of the system
/// admin who granted them system admin, null for the platform's grant and for a grant before the column, and every membership by the
/// tenant's name, then id.
/// </summary>
public sealed record UserDetailView(
    UserView User,
    DateTimeOffset? SuspendedAt,
    UserNameView? SuspendedBy,
    string? SuspensionReason,
    UserNameView? SystemAdminGrantedBy,
    IReadOnlyList<UserMembershipView> Memberships);
