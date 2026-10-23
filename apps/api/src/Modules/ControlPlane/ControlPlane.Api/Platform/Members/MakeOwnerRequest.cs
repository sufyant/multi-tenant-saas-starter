using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Members;

/// <summary>
/// The id of the user to make an Owner, a member of the tenant, as the user's detail lists the user's memberships. It is required: a
/// request without one is refused, so it never falls back to an empty id.
/// </summary>
public sealed record MakeOwnerRequest([property: JsonRequired] Guid UserId);
