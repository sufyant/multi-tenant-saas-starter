using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// A plan belongs to no tenant, so the platform reads every one (R6).
internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> plan)
    {
        plan.Property(p => p.Id).ValueGeneratedNever();
        plan.Property(p => p.Name).HasMaxLength(Plan.NameMaxLength);
        plan.Ignore(p => p.ValueMap);
        plan.HasMany(p => p.Values).WithOne().HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Restrict);
        plan.Navigation(p => p.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
        plan.Ignore(p => p.MeterMap);
        plan.Ignore(p => p.Content);
        plan.HasMany(p => p.Meters).WithOne().HasForeignKey(meter => meter.PlanId).OnDelete(DeleteBehavior.Restrict);
        plan.Navigation(p => p.Meters).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Step 40: the prices are columns of the plan, each a whole number of minor units, 0 to PlanPrices.Maximum (R15).
        plan.ComplexProperty(p => p.Prices, prices =>
        {
            prices.Property(price => price.BaseMonthly).HasColumnName("base_monthly");
            prices.Property(price => price.PerPersonMonthly).HasColumnName("per_person_monthly");
            prices.Property(price => price.BaseYearly).HasColumnName("base_yearly");
            prices.Property(price => price.PerPersonYearly).HasColumnName("per_person_yearly");
        });
        plan.ToTable(table => table.HasCheckConstraint(
            "ck_plans_prices",
            $"base_monthly BETWEEN 0 AND {PlanPrices.Maximum} AND per_person_monthly BETWEEN 0 AND {PlanPrices.Maximum} "
            + $"AND base_yearly BETWEEN 0 AND {PlanPrices.Maximum} AND per_person_yearly BETWEEN 0 AND {PlanPrices.Maximum}"));

        // A plan's name is unique without regard to case, as a role's is. The command checks it with lower() under the plan's lock; the unique
        // index ix_plans_lower_name is the second guard (K6b). EF Core cannot model an index on an expression, so the migration writes it in
        // SQL, and the model does not hold it.

        // K5: the plan list searches the name with a trigram index and sorts by it with a B-tree index.
        plan.HasIndex(p => p.Name);
        plan.HasIndex(p => p.Name, "ix_plans_name_trgm").HasDatabaseName("ix_plans_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
