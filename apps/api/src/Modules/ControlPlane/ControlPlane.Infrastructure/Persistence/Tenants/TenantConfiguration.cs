using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Users;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> tenant)
    {
        tenant.Property(t => t.Id).ValueGeneratedNever();
        tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
        tenant.Property(t => t.Slug).HasMaxLength(63);
        tenant.HasIndex(t => t.Slug).IsUnique();
        tenant.HasIndex(t => t.Name);
        tenant.HasIndex(t => t.Name, "ix_tenants_name_trgm").HasDatabaseName("ix_tenants_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        tenant.HasIndex(t => t.Slug, "ix_tenants_slug_trgm").HasDatabaseName("ix_tenants_slug_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        tenant.Property(t => t.SuspensionReason).HasMaxLength(Tenant.SuspensionReasonMaxLength);

        // The tenant's plan: a tenant always has one, and a plan a tenant is on cannot be deleted (R16).
        tenant.HasOne<Plan>().WithMany().HasForeignKey(t => t.PlanId).OnDelete(DeleteBehavior.Restrict);

        // The system admins of its events. A user row stays after the user's deletion (A8), so a key never blocks it.
        tenant.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        tenant.HasOne<User>().WithMany().HasForeignKey(t => t.SuspendedBy).OnDelete(DeleteBehavior.Restrict);
        tenant.HasOne<User>().WithMany().HasForeignKey(t => t.ClosedBy).OnDelete(DeleteBehavior.Restrict);

        // R15: a status the code knows, a purged tenant is its tombstone: the moment of its purge and none of the closure, a suspension or
        // its creator. A suspension's moment and system admin go together, and they come with its reason; a reason may stand alone, from
        // before step 37b. A closure's system admin comes with its moment, and the start of a purge stays only while the tenant purges.
        tenant.ToTable(table => table.HasCheckConstraint("ck_tenants_status", CheckConstraints.OneOf<TenantStatus>("status")));
        tenant.ToTable(table => table.HasCheckConstraint(
            "ck_tenants_purged_is_a_tombstone",
            "status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL AND created_by IS NULL)"));
        tenant.ToTable(table => table.HasCheckConstraint(
            "ck_tenants_suspension",
            "(suspended_at IS NULL) = (suspended_by IS NULL) AND (suspended_at IS NULL OR suspension_reason IS NOT NULL)"));
        tenant.ToTable(table => table.HasCheckConstraint("ck_tenants_closed_by_with_closure", "closed_by IS NULL OR closed_at IS NOT NULL"));
        tenant.ToTable(table => table.HasCheckConstraint("ck_tenants_purge_started_while_purging", "purge_started_at IS NULL OR status = 'Purging'"));
    }
}
