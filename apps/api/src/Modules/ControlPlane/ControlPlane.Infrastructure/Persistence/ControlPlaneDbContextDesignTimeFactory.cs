using Microsoft.EntityFrameworkCore.Design;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence;

// Lets `dotnet ef migrations add` build the model without starting the host; it never connects.
internal sealed class ControlPlaneDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args) =>
        new(TenancyServiceCollectionExtensions.ModuleDbContextOptions<ControlPlaneDbContext>(ControlPlaneDbContext.Schema, "Host=design-time"));
}
