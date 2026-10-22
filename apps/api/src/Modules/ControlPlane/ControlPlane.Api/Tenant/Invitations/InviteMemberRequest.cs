using System.Text.Json.Serialization;
using SharedKernel;

namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// The address to invite, the id of the tenant's role the invitation gives, and the language of the invitation's email: without one,
/// the inviter's own. The role is required: a request without one is refused, so it never falls back to an empty id.
/// </summary>
public sealed record InviteMemberRequest(string Email, [property: JsonRequired] Guid RoleId, Language? Language = null);
