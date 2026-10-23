using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        // The database's defaults: a row inserted without these columns is a user from the moment of the insert, with no membership
        // (R13).
        user.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        user.Property(u => u.MembershipCount).HasDefaultValue(0);
    }
}
