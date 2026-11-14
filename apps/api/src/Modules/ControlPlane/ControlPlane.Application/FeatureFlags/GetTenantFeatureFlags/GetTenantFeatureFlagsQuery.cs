using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetTenantFeatureFlags;

/// <summary>
/// The flags that are on for the route's organisation, each with the first rule that turns it on, for its page in the platform (4.09). It
/// runs in the tenant of the platform's route (W3).
/// </summary>
[MessageName("get-tenant-feature-flags")]
public sealed record GetTenantFeatureFlagsQuery;
