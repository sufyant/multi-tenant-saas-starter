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

        // A plan's name is unique without regard to case, as a role's is. The command checks it with lower() under the plan's lock; the unique
        // index ix_plans_lower_name is the second guard (K6b). EF Core cannot model an index on an expression, so the migration writes it in
        // SQL, and the model does not hold it.

        // K5: the plan list searches the name with a trigram index and sorts by it with a B-tree index.
        plan.HasIndex(p => p.Name);
        plan.HasIndex(p => p.Name, "ix_plans_name_trgm").HasDatabaseName("ix_plans_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
