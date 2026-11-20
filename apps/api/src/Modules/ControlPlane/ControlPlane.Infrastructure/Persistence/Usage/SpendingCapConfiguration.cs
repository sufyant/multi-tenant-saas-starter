using ControlPlane.Domain.SpendingCaps;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Usage;

// The tenant's spending cap (step 40c): a tenant table under row level security (R1), one row for each tenant that has a cap, keyed by the
// tenant column, which the database fills from the transaction's tenant. The amount is in its range, and the month it was reached goes with
// its moment and is the month it neared it (R15).
internal sealed class SpendingCapConfiguration : IEntityTypeConfiguration<SpendingCap>
{
    public void Configure(EntityTypeBuilder<SpendingCap> cap)
    {
        cap.ToTable("spending_caps");
        cap.Property<Guid>("TenantId");
        cap.HasKey("TenantId");
        cap.HasOne<Tenant>().WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
        cap.ToTable(table => table.HasCheckConstraint("ck_spending_caps_amount", $"amount BETWEEN 1 AND {SpendingCap.Maximum}"));
        cap.ToTable(table => table.HasCheckConstraint(
            "ck_spending_caps_reached",
            "(reached_period IS NULL) = (reached_at IS NULL) AND (reached_period IS NULL OR extract(day FROM reached_period) = 1)"));
        cap.ToTable(table => table.HasCheckConstraint(
            "ck_spending_caps_nearly_reached",
            "(nearly_reached_period IS NULL OR extract(day FROM nearly_reached_period) = 1) AND (reached_period IS NULL OR nearly_reached_period IS NOT DISTINCT FROM reached_period)"));
    }
}
