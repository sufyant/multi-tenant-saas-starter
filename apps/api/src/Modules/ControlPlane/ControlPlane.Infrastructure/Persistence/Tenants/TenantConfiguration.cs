using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        tenant.Property(t => t.CancellationReason).HasMaxLength(Tenant.CancellationReasonMaxLength);
        tenant.Property(t => t.SuspensionReason).HasMaxLength(Tenant.SuspensionReasonMaxLength);

        // R12: the summary the platform's tenant list reads across tenants, where row level security hides the memberships and the
        // invitations. Tenant.Summarize writes it under the tenant's lock. TenantRepository inserts a new tenant with SQL, without the
        // summary, which the defaults make empty.
        tenant.Property(t => t.MemberCount).HasDefaultValue(0);
        tenant.Property(t => t.OwnerCount).HasDefaultValue(0);
        tenant.HasOne<User>().WithMany().HasForeignKey(t => t.FirstOwnerId).OnDelete(DeleteBehavior.Restrict);
        tenant.Property(t => t.OwnerInvitationEmail).HasMaxLength(Invitation.EmailMaxLength);
        tenant.Property(t => t.OwnerInvitationStatus).HasConversion<string>().HasMaxLength(20);
    }
}
