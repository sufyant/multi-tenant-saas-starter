using System.Text.Json.Serialization;
using ControlPlane.Contracts.Members;

namespace ControlPlane.Api.Tenant.Members;

/// <summary>
/// The built-in role to give the member. The role is required: a request without one is refused, so it never falls back to the enum's
/// first value.
/// </summary>
public sealed record ChangeMemberRoleRequest([property: JsonRequired] MemberRole Role);
