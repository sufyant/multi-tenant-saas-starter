using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// Who invites, as <see cref="Tenant.Invite"/> decides by it: a member of the tenant with their role, or the platform when a system admin
/// invites the tenant's Owner. The invitation takes the inviter's language when the request names none (K8).
/// </summary>
internal sealed record Inviter(Guid UserId, Language Language, Role? MemberRole)
{
    public static Inviter Member(Guid userId, Language language, Role role) => new(userId, language, role);

    public static Inviter Platform(Guid userId, Language language) => new(userId, language, null);
}
