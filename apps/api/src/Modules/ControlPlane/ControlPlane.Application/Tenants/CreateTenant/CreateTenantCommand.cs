using SharedKernel;

namespace ControlPlane.Application.Tenants.CreateTenant;

/// <summary>
/// A system admin creates a tenant. It runs inside the tenant it creates, whose id the caller chose, so every step
/// of the saga runs in that tenant's transaction and hands the tenant on to the next one. The host's idempotency layer protects
/// the HTTP request (section 3). The command carries no request key. The owner's invitation email is in the language given, else in
/// the system admin's own.
/// </summary>
[MessageName("create-tenant")]
public sealed record CreateTenantCommand(string ActorId, string Name, string Slug, string OwnerEmail, Language? Language);
