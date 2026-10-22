using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// R12: the summary the platform's tenant list reads across tenants, where row level security hides the memberships and the invitations.
// One row for each tenant, keyed by it. It is not a tenant entity: the platform reads every row, as it reads the tenants (R6).
// TenantRepository inserts a new tenant's row with SQL, without the summary, which the defaults make empty.
internal sealed class TenantSummaryConfiguration : IEntityTypeConfiguration<TenantSummary>
{
    public void Configure(EntityTypeBuilder<TenantSummary> summary)
    {
        summary.ToTable("tenant_summaries");
        summary.HasKey(s => s.TenantId);
        summary.Property(s => s.TenantId).ValueGeneratedNever();
        summary.HasOne<Tenant>().WithOne().HasForeignKey<TenantSummary>(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        summary.Property(s => s.MemberCount).HasDefaultValue(0);
        summary.Property(s => s.OwnerCount).HasDefaultValue(0);
        summary.Property(s => s.OwnerInvitationEmail).HasMaxLength(Invitation.EmailMaxLength);
        summary.Property(s => s.OwnerInvitationStatus).HasConversion<string>().HasMaxLength(20);
        summary.Property(s => s.OwnerInvitationLanguage).HasConversion(ControlPlaneDbContext.LanguageTag).HasMaxLength(ControlPlaneDbContext.LanguageTagMaxLength);
    }
}
