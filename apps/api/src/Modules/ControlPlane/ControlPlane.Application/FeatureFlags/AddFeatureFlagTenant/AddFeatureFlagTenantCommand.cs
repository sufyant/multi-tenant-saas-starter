using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.AddFeatureFlagTenant;

/// <summary>A system admin lists the flag's first rule an organisation (12.08, 12.09).</summary>
[MessageName("add-feature-flag-tenant")]
public sealed record AddFeatureFlagTenantCommand(string ActorId, string Key, Guid TenantId);
