using SharedKernel;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A new tenant, the email address of its first owner, who is invited once the tenant is ready, and the language of the owner's
/// invitation email: without one, the system admin's own. The plan the tenant goes on: without one, the default plan. The API trims the
/// name before it counts its length.
/// </summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail, Language? Language = null, Guid? PlanId = null);
