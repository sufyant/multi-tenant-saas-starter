using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

// A feature flag of this environment belongs to no tenant: its rules reach every organisation, and only the platform changes them (R6).
internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    // A key is as long as a permission may be.
    public const int KeyMaxLength = 100;

    public void Configure(EntityTypeBuilder<FeatureFlag> flag)
    {
        flag.Property(f => f.Id).ValueGeneratedNever();
        flag.Property(f => f.Key).HasMaxLength(KeyMaxLength);
        flag.Property(f => f.Type).HasConversion<string>().HasMaxLength(20);
        flag.Property(f => f.Description).HasMaxLength(FeatureFlag.DescriptionMaxLength);
        flag.Property(f => f.AddedRevision).HasMaxLength(FeatureFlag.RevisionMaxLength);
        flag.Property(f => f.RemovedRevision).HasMaxLength(FeatureFlag.RevisionMaxLength);
        flag.Ignore(f => f.IsRemovedFromCode);

        // A key names one flag for good: an archived flag keeps its key, so no later flag takes it (K6b).
        flag.HasIndex(f => f.Key).IsUnique();
        flag.HasOne<User>().WithMany().HasForeignKey(f => f.LastChangedBy).OnDelete(DeleteBehavior.Restrict);

        // R15: a known type; a percentage from 0 to 100; a count of listed organisations of at least 0; an archive's day and commit
        // only with its day; a kill switch is on by default and a release flag off.
        flag.ToTable(table => table.HasCheckConstraint("ck_feature_flags_type", CheckConstraints.OneOf<FeatureFlagType>("type")));
        flag.ToTable(table => table.HasCheckConstraint("ck_feature_flags_percentage", $"percentage BETWEEN 0 AND {FeatureFlag.MaxPercentage}"));
        flag.ToTable(table => table.HasCheckConstraint("ck_feature_flags_listed_count", "listed_count >= 0"));
        flag.ToTable(table => table.HasCheckConstraint("ck_feature_flags_removal", "removed_at IS NOT NULL OR removed_revision IS NULL"));
        flag.ToTable(table => table.HasCheckConstraint(
            "ck_feature_flags_default_value", $"default_value = (type = '{nameof(FeatureFlagType.KillSwitch)}')"));
    }
}
