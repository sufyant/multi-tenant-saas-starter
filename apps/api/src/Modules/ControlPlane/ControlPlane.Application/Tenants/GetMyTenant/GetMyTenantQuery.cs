namespace ControlPlane.Application.Tenants.GetMyTenant;

/// <summary>
/// The tenant of the route, for one of its members: the app shell and Settings › General. The tenant door has let the member in, so the
/// tenant is active.
/// </summary>
public sealed record GetMyTenantQuery(string ActorId);
