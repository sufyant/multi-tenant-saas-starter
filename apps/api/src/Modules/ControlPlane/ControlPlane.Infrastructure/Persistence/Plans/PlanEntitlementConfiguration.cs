using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// A plan's value for one key. A key the plan has no row for is closed. The row goes when an edit removes the value; the plan's key restricts
// any other deletion (R16).
internal sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> value)
    {
        value.HasKey(v => new { v.PlanId, v.Key });
        value.Ignore(v => v.Value);
        EntitlementColumns.Configure(value, "plan_entitlements");
    }
}
