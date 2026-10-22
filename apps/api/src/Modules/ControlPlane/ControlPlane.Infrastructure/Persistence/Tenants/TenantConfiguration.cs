using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> tenant)
    {
        tenant.Property(t => t.Id).ValueGeneratedNever();
        tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
        tenant.Property(t => t.Slug).HasMaxLength(63);
        tenant.HasIndex(t => t.Slug).IsUnique();
        tenant.HasIndex(t => t.Name);
        tenant.HasIndex(t => t.Name, "ix_tenants_name_trgm").HasDatabaseName("ix_tenants_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        tenant.HasIndex(t => t.Slug, "ix_tenants_slug_trgm").HasDatabaseName("ix_tenants_slug_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        tenant.Property(t => t.SuspensionReason).HasMaxLength(Tenant.SuspensionReasonMaxLength);
    }
}
