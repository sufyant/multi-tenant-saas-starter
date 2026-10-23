using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Invitations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> invitation)
    {
        invitation.Property(i => i.Id).ValueGeneratedNever();
        invitation.Property(i => i.Email).HasMaxLength(EmailAddress.MaxLength);
        invitation.Property(i => i.SecretHash).HasMaxLength(64);
        invitation.Property(i => i.SignUpInvitationId).HasMaxLength(255);
        invitation.HasIndex(i => i.SecretHash).IsUnique();
        // K2: one pending invitation for each tenant and address. The address is stored in its one form (EmailAddress), so the index
        // is on the stored value.
        invitation.HasIndex(i => new { i.TenantId, i.Email }, "ix_invitations_pending_email").HasDatabaseName("ix_invitations_pending_email").IsUnique().HasFilter("status = 'Pending'");
        invitation.HasIndex(i => i.ExpiresAt);
        invitation.HasIndex(i => i.Email);
        invitation.HasIndex(i => i.Email, "ix_invitations_email_trgm").HasDatabaseName("ix_invitations_email_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        invitation.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        invitation.Property(i => i.Language).HasConversion(ControlPlaneDbContext.LanguageTag).HasMaxLength(ControlPlaneDbContext.LanguageTagMaxLength);
        invitation.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId);
        // A2: a role of the invitation's own tenant, as a membership's. A finished invitation keeps no role once its custom role is deleted:
        // the migration KeepFinishedInvitationsOfADeletedRole writes the key ON DELETE SET NULL (role_id), which nulls the role and keeps the
        // tenant (PostgreSQL 15 and later). EF Core cannot model a column list there, so the model says NoAction and the database does the
        // rest. A pending invitation always has a role (ck_invitations_pending_has_role), and a role in use cannot be deleted (A3).
        invitation.HasOne<Role>().WithMany().HasForeignKey(i => new { i.TenantId, i.RoleId }).HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        invitation.ToTable(table => table.HasCheckConstraint("ck_invitations_pending_has_role", "status <> 'Pending' OR role_id IS NOT NULL"));
        // A8: a user's deletion erases the address of a finished invitation only. Pending is the stored status, expired or not: a CHECK
        // cannot read the clock, and an expired pending invitation is still the link the email sent.
        invitation.ToTable(table => table.HasCheckConstraint("ck_invitations_pending_has_email", "status <> 'Pending' OR email IS NOT NULL"));
        // R15: a status and a language the code knows, and an accepted invitation, and only an accepted one, has its moment and its user.
        invitation.ToTable(table => table.HasCheckConstraint("ck_invitations_status", CheckConstraints.OneOf<InvitationStatus>("status")));
        invitation.ToTable(table => table.HasCheckConstraint("ck_invitations_language", CheckConstraints.OneOf("language", ControlPlaneDbContext.LanguageTags)));
        invitation.ToTable(table => table.HasCheckConstraint(
            "ck_invitations_acceptance", "(accepted_at IS NULL) = (accepted_by IS NULL) AND (status = 'Accepted') = (accepted_at IS NOT NULL)"));
        invitation.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedBy).OnDelete(DeleteBehavior.Restrict);
        invitation.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
