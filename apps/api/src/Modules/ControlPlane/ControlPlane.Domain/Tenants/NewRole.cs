namespace ControlPlane.Domain.Tenants;

/// <summary>The custom role a request asks for (<see cref="Tenant.CreateRole"/>): its id, its name and its permissions.</summary>
internal sealed record NewRole(Guid Id, string Name, IReadOnlyCollection<string> Permissions);
