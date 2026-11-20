using ControlPlane.Domain.Plans;
using ControlPlane.Domain.MeterUsages;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Usage;

// A tenant's use of a meter in a month (step 40c): a tenant table under row level security (R1), keyed by the tenant, the meter and the
// month's first day, so the key indexes the tenant too. The meter key is not checked by a constraint, as plan_meters' is not: the meters are
// the code's. The counts never go below 0, and the extra is never more than the count, and the count reaches 80% of the quota no later than all of it (R15).
internal sealed class MeterUsageConfiguration : IEntityTypeConfiguration<MeterUsage>
{
    public void Configure(EntityTypeBuilder<MeterUsage> usage)
    {
        usage.ToTable("meter_usages");
        usage.HasKey(u => new { u.TenantId, u.MeterKey, u.Period });
        usage.Property(u => u.MeterKey).HasMaxLength(100);
        usage.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        usage.ToTable(table => table.HasCheckConstraint(
            "ck_meter_usages_counts",
            $"used >= 0 AND extra BETWEEN 0 AND used AND unit_price BETWEEN 0 AND {PlanPrices.Maximum} AND unit_size BETWEEN 1 AND {PlanMeter.Maximum}"));
        usage.ToTable(table => table.HasCheckConstraint("ck_meter_usages_period", "extract(day FROM period) = 1"));
        usage.ToTable(table => table.HasCheckConstraint("ck_meter_usages_notices", "quota_reached_at IS NULL OR nearly_used_at IS NOT NULL AND nearly_used_at <= quota_reached_at"));
    }
}
