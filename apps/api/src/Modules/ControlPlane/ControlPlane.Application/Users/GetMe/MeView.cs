using SharedKernel;

namespace ControlPlane.Application.Users.GetMe;

/// <summary>
/// The signed-in user: our user id, the copy of their profile, whether they are a system admin, their preferences, and how many tenants
/// they created that were not purged, which the limit of sign-up counts (step 40d).
/// </summary>
public sealed record MeView(Guid Id, string? Email, string? Name, bool IsSystemAdmin, Language Language, string? TimeZone, int CreatedTenantCount);
