using ControlPlane.Application.Plans;
using ControlPlane.Application.Users;

namespace ControlPlane.Application.Tenants.GetTenantPlan;

/// <summary>
/// A tenant's override of one key: its value, its expiry, whether it has expired at the moment of the read (it then applies no more, and
/// stays until a system admin removes it), the system admin's reason, who set it and when.
/// </summary>
public sealed record EntitlementOverrideView(
    EntitlementValueView Value,
    DateTimeOffset? ExpiresAt,
    bool Expired,
    string Reason,
    Guid SetById,
    UserNameView SetBy,
    DateTimeOffset SetAt);
