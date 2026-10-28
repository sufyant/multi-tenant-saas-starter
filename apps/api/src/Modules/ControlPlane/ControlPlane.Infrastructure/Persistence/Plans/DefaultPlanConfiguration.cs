using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// The default mark is one row whose key can only be true, so the database holds exactly one default plan: never none, never two. The plan
// it names cannot be deleted under it (R16).
internal sealed class DefaultPlanConfiguration : IEntityTypeConfiguration<DefaultPlan>
{
    public void Configure(EntityTypeBuilder<DefaultPlan> mark)
    {
        mark.ToTable("default_plans");
        mark.HasKey(m => m.Id);
        mark.Property(m => m.Id).ValueGeneratedNever();
        mark.HasOne<Plan>().WithMany().HasForeignKey(m => m.PlanId).OnDelete(DeleteBehavior.Restrict);
        mark.ToTable(table => table.HasCheckConstraint("ck_default_plans_one_row", "id"));
    }
}
