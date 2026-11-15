using Microsoft.EntityFrameworkCore.Design;
using Tenancy;

namespace Billing.Infrastructure.Persistence;

// Lets `dotnet ef migrations add` build the model without starting the host; it never connects.
internal sealed class BillingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args) =>
        new(TenancyServiceCollectionExtensions.ModuleDbContextOptions<BillingDbContext>(BillingDbContext.Schema, "Host=design-time"));
}
