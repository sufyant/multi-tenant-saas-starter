using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

// An organisation that a flag's first rule lists. Only the platform writes it, and the evaluation and the previews read it across tenants,
// as they read an entitlement override. So it is not a tenant entity: it is outside row level security (R6). The tenant's purge deletes it.
internal sealed class FeatureFlagTenantConfiguration : IEntityTypeConfiguration<FeatureFlagTenant>
{
    public void Configure(EntityTypeBuilder<FeatureFlagTenant> listed)
    {
        listed.HasKey(l => new { l.FlagId, l.TenantId });
        listed.HasOne<FeatureFlag>().WithMany().HasForeignKey(l => l.FlagId).OnDelete(DeleteBehavior.Restrict);
        listed.HasOne<Tenant>().WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Restrict);
        listed.HasOne<User>().WithMany().HasForeignKey(l => l.ListedBy).OnDelete(DeleteBehavior.Restrict);

        // The evaluation reads the flags that list one organisation, and its purge deletes them.
        listed.HasIndex(l => l.TenantId);
    }
}
