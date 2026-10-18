using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Invitations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> invitation)
    {
        invitation.Property(i => i.Id).ValueGeneratedNever();
        invitation.Property(i => i.Email).HasMaxLength(Invitation.EmailMaxLength);
        invitation.Property(i => i.SecretHash).HasMaxLength(64);
        invitation.Property(i => i.SignUpInvitationId).HasMaxLength(255);
        invitation.HasIndex(i => i.SecretHash).IsUnique();
        // The partial unique index ix_invitations_pending_email keeps one pending invitation for each tenant and lower(email) (K2).
        // It is on an expression, which EF Core does not model, so the migration AddInvitationIndexes creates it in SQL.
        invitation.HasIndex(i => i.ExpiresAt);
        invitation.HasIndex(i => i.Email);
        invitation.HasIndex(i => i.Email, "ix_invitations_email_trgm").HasDatabaseName("ix_invitations_email_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        invitation.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        invitation.Property(i => i.Language).HasConversion(ControlPlaneDbContext.LanguageTag).HasMaxLength(ControlPlaneDbContext.LanguageTagMaxLength);
        invitation.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId);
        invitation.HasOne<Role>().WithMany().HasForeignKey(i => i.RoleId).OnDelete(DeleteBehavior.Cascade);
        invitation.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedBy).OnDelete(DeleteBehavior.Restrict);
        invitation.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
