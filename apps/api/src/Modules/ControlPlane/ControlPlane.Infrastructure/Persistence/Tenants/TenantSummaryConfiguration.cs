using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

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
        summary.Property(s => s.OwnerInvitationEmail).HasMaxLength(EmailAddress.MaxLength);
        summary.Property(s => s.OwnerInvitationStatus).HasConversion<string>().HasMaxLength(20);
        summary.Property(s => s.OwnerInvitationLanguage).HasConversion(ControlPlaneDbContext.LanguageTag).HasMaxLength(ControlPlaneDbContext.LanguageTagMaxLength);

        // R15: the counts, and the latest Owner invitation only while the tenant has no Owner. Its status and expiry are there together.
        // The address may be gone from a finished invitation (A8), and the language from a row an older version wrote (section 13), so
        // only those two are kept apart; a pending invitation always has its address.
        summary.ToTable(table => table.HasCheckConstraint("ck_tenant_summaries_counts", "member_count >= 0 AND owner_count >= 0 AND member_count >= owner_count"));
        summary.ToTable(table => table.HasCheckConstraint(
            "ck_tenant_summaries_owner_invitation_status", CheckConstraints.OneOf<InvitationStatus>("owner_invitation_status")));
        summary.ToTable(table => table.HasCheckConstraint(
            "ck_tenant_summaries_owner_invitation_language", CheckConstraints.OneOf("owner_invitation_language", ControlPlaneDbContext.LanguageTags)));
        summary.ToTable(table => table.HasCheckConstraint(
            "ck_tenant_summaries_owner_invitation_together",
            "(owner_invitation_status IS NULL) = (owner_invitation_expires_at IS NULL) "
            + "AND (owner_invitation_status IS NOT NULL OR (owner_invitation_email IS NULL AND owner_invitation_language IS NULL))"));
        summary.ToTable(table => table.HasCheckConstraint(
            "ck_tenant_summaries_pending_owner_invitation_has_email", "owner_invitation_status <> 'Pending' OR owner_invitation_email IS NOT NULL"));
        summary.ToTable(table => table.HasCheckConstraint(
            "ck_tenant_summaries_owner_invitation_without_owner", "owner_count = 0 OR owner_invitation_status IS NULL"));
    }
}
