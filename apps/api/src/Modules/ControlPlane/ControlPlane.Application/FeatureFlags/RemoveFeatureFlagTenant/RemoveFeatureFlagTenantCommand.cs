using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.RemoveFeatureFlagTenant;

/// <summary>A system admin removes from the flag's first rule an organisation (12.08, 12.09).</summary>
[MessageName("remove-feature-flag-tenant")]
public sealed record RemoveFeatureFlagTenantCommand(string ActorId, string Key, Guid TenantId);
