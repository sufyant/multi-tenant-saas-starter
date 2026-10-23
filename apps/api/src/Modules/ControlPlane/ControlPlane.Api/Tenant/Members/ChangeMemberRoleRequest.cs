using System.Text.Json.Serialization;

namespace ControlPlane.Api.Tenant.Members;

/// <summary>
/// The id of the tenant's role to give the member. The role is required: a request without one is refused, so it never falls back to
/// an empty id.
/// </summary>
public sealed record ChangeMemberRoleRequest([property: JsonRequired] Guid RoleId);
