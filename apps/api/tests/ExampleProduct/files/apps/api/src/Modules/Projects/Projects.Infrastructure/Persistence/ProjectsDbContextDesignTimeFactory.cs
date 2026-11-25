using Microsoft.EntityFrameworkCore.Design;
using Tenancy;

namespace Projects.Infrastructure.Persistence;

// Lets `dotnet ef migrations add` build the model without starting the host; it never connects.
internal sealed class ProjectsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ProjectsDbContext>
{
    public ProjectsDbContext CreateDbContext(string[] args) =>
        new(TenancyServiceCollectionExtensions.ModuleDbContextOptions<ProjectsDbContext>(ProjectsDbContext.Schema, "Host=design-time"));
}
