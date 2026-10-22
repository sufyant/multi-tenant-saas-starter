using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// The invitation a request asks for (<see cref="Tenant.Invite"/>): its id, the address, the role, the language the request names, if
/// any, the secret its link carries, the moment and the lifetime.
/// </summary>
internal sealed record NewInvitation(
    Guid Id,
    EmailAddress Email,
    Role Role,
    Language? Language,
    string Secret,
    DateTimeOffset Now,
    TimeSpan Lifetime);
