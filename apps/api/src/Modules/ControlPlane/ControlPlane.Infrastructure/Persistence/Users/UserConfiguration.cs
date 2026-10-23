using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> user)
    {
        user.Property(u => u.Id).ValueGeneratedNever();
        user.Property(u => u.ExternalId).HasMaxLength(255);
        user.HasIndex(u => u.ExternalId).IsUnique();
        user.HasIndex(u => u.Email);
        user.HasIndex(u => u.LastName);
        user.HasIndex(u => u.Email, "ix_users_email_trgm").HasDatabaseName("ix_users_email_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        user.HasIndex(u => u.FirstName, "ix_users_first_name_trgm").HasDatabaseName("ix_users_first_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        user.HasIndex(u => u.LastName, "ix_users_last_name_trgm").HasDatabaseName("ix_users_last_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        user.Property(u => u.Language).HasConversion(ControlPlaneDbContext.LanguageTag).HasMaxLength(ControlPlaneDbContext.LanguageTagMaxLength);
        user.Property(u => u.TimeZone).HasMaxLength(User.TimeZoneMaxLength);
        user.HasOne<User>().WithMany().HasForeignKey(u => u.SuspendedBy).OnDelete(DeleteBehavior.Restrict);

        // R15: a language of the one list, a count that never falls below zero, a suspension that names its system admin, and a deleted
        // user who keeps no address, name or time zone (A8).
        user.ToTable(table => table.HasCheckConstraint("ck_users_language", CheckConstraints.OneOf("language", ControlPlaneDbContext.LanguageTags)));
        user.ToTable(table => table.HasCheckConstraint("ck_users_membership_count", "membership_count >= 0"));
        user.ToTable(table => table.HasCheckConstraint("ck_users_suspension", "(suspended_at IS NULL) = (suspended_by IS NULL)"));
        user.ToTable(table => table.HasCheckConstraint(
            "ck_users_deleted_keeps_no_personal_data",
            "deleted_at IS NULL OR (email IS NULL AND first_name IS NULL AND last_name IS NULL AND time_zone IS NULL)"));

        // The database's defaults: a row inserted without these columns is a user from the moment of the insert, with no membership
        // (R13).
        user.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        user.Property(u => u.MembershipCount).HasDefaultValue(0);
    }
}
