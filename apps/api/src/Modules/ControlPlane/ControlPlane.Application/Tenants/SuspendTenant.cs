namespace ControlPlane.Application.Tenants;

public sealed record SuspendTenant(string AdminId);

public static class SuspendTenantHandler
{
    public static Task HandleAsync(SuspendTenant command) => throw new NotImplementedException();
}
