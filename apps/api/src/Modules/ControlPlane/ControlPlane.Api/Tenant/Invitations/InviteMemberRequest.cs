using System.Text.Json.Serialization;
using ControlPlane.Contracts.Members;
using SharedKernel;

namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// The address to invite, the built-in role the invitation gives, and the language of the invitation's email: without one, the
/// inviter's own. The role is required: a request without one is refused, so it never falls back to the enum's first value.
/// </summary>
public sealed record InviteMemberRequest(string Email, [property: JsonRequired] MemberRole Role, Language? Language = null);
