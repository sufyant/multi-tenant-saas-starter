using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A tenant's override of one key: its value, its expiry (null for one that never expires), its state, the system admin's reason, the
/// system admin who set it, by id and by name, and when.
/// </summary>
public sealed record EntitlementOverrideResponse(
    EntitlementValueResponse Value,
    DateTimeOffset? ExpiresAt,
    EntitlementOverrideResponseState State,
    string Reason,
    Guid SetById,
    UserNameResponse SetBy,
    DateTimeOffset SetAt);
