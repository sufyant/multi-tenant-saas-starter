using ControlPlane.Domain.Files;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Files;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    // The longest object key: the key of a user's export, with two ids and its folder and extension, fits with room to spare.
    public const int ObjectKeyMaxLength = 255;

    public void Configure(EntityTypeBuilder<StoredFile> file)
    {
        file.Property(f => f.Id).ValueGeneratedNever();
        file.Property(f => f.Module).HasMaxLength(50);
        file.Property(f => f.Purpose).HasConversion<string>().HasMaxLength(30);
        file.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        file.Property(f => f.ObjectKey).HasMaxLength(ObjectKeyMaxLength);
        file.Property(f => f.ContentType).HasMaxLength(100);
        file.HasIndex(f => f.ObjectKey).IsUnique();
        file.HasIndex(f => new { f.UserId, f.CreatedAt });
        file.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);

        // R15: a known purpose and status; a ready file has its size and its expiry, and a file in preparation has neither.
        file.ToTable(table => table.HasCheckConstraint("ck_stored_files_purpose", CheckConstraints.OneOf<StoredFilePurpose>("purpose")));
        file.ToTable(table => table.HasCheckConstraint("ck_stored_files_status", CheckConstraints.OneOf<StoredFileStatus>("status")));
        file.ToTable(table => table.HasCheckConstraint(
            "ck_stored_files_ready_has_size_and_expiry",
            "status = 'Preparing' OR status = 'Failed' OR (size_bytes IS NOT NULL AND ready_at IS NOT NULL AND expires_at IS NOT NULL)"));
    }
}
