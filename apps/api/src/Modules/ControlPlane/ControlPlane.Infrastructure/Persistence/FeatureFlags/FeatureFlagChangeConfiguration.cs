using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

// A line of a flag's history belongs to the flag, so to no tenant (R6). A line that names an organisation keeps its id; the organisation's
// row stays as a tombstone after its purge (docs/flows/tenant-purge.md), so the key never blocks the purge.
internal sealed class FeatureFlagChangeConfiguration : IEntityTypeConfiguration<FeatureFlagChange>
{
    public void Configure(EntityTypeBuilder<FeatureFlagChange> change)
    {
        change.Property(c => c.Id).ValueGeneratedNever();
        change.Property(c => c.Kind).HasConversion<string>().HasMaxLength(30);
        change.Property(c => c.From).HasMaxLength(10);
        change.Property(c => c.To).HasMaxLength(10);
        change.Property(c => c.Revision).HasMaxLength(FeatureFlag.RevisionMaxLength);
        change.HasOne<FeatureFlag>().WithMany().HasForeignKey(c => c.FlagId).OnDelete(DeleteBehavior.Restrict);
        change.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        change.HasOne<User>().WithMany().HasForeignKey(c => c.ActorId).OnDelete(DeleteBehavior.Restrict);

        // A flag's page reads its history newest first.
        change.HasIndex(c => new { c.FlagId, c.At });

        // R15: a known kind; a deploy's line names no person, and a rule's line names its system admin.
        change.ToTable(table => table.HasCheckConstraint("ck_feature_flag_changes_kind", CheckConstraints.OneOf<FeatureFlagChangeKind>("kind")));
        change.ToTable(table => table.HasCheckConstraint(
            "ck_feature_flag_changes_actor",
            $"(kind IN ('{nameof(FeatureFlagChangeKind.AddedToCode)}', '{nameof(FeatureFlagChangeKind.RemovedFromCode)}')) = (actor_id IS NULL)"));
        change.ToTable(table => table.HasCheckConstraint(
            "ck_feature_flag_changes_tenant",
            $"(kind IN ('{nameof(FeatureFlagChangeKind.Listed)}', '{nameof(FeatureFlagChangeKind.Unlisted)}')) = (tenant_id IS NOT NULL)"));
    }
}
