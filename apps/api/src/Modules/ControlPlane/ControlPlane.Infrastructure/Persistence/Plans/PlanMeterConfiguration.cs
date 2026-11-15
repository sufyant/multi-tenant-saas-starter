using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// A plan's setting of one meter (step 40). A meter the plan has no row for is not on the plan. The row goes when an edit removes the meter;
// the plan's key restricts any other deletion (R16). A plan belongs to no tenant, so its meters do not either (R6). The key is not checked
// by a constraint: the meters are the code's (MeterCatalog), and a product adds one without a migration; the command checks it.
internal sealed class PlanMeterConfiguration : IEntityTypeConfiguration<PlanMeter>
{
    public void Configure(EntityTypeBuilder<PlanMeter> meter)
    {
        meter.ToTable("plan_meters");
        meter.HasKey(m => new { m.PlanId, m.Key });
        meter.Property(m => m.Key).HasMaxLength(100);
        meter.Ignore(m => m.Setting);
        meter.Property(m => m.Overage).HasConversion<string>().HasMaxLength(20);

        // R15: the code's ranges.
        meter.ToTable(table => table.HasCheckConstraint("ck_plan_meters_overage", CheckConstraints.OneOf<MeterOverage>("overage")));
        meter.ToTable(table => table.HasCheckConstraint(
            "ck_plan_meters_ranges",
            $"quota BETWEEN 0 AND {PlanMeter.Maximum} AND unit_price BETWEEN 0 AND {PlanPrices.Maximum} AND unit_size BETWEEN 1 AND {PlanMeter.Maximum}"));
    }
}
