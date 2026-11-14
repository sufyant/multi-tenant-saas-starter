using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetMyTenantFeatureFlags;

/// <summary>
/// The flags of the route's organisation, which the web's screens ask (docs/flows/feature-flags.md). It runs in the tenant that the tenant
/// door proved, and reads it from the envelope (T2, W3).
/// </summary>
[MessageName("get-my-tenant-feature-flags")]
public sealed record GetMyTenantFeatureFlagsQuery;
