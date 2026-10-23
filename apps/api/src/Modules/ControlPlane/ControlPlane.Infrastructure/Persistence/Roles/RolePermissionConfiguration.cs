using ControlPlane.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> grant)
    {
        // A3: a custom role holds each permission once. The tenant leads the key, as in every key of a tenant's row.
        grant.HasKey(g => new { g.TenantId, g.RoleId, g.Permission });
        grant.Property(g => g.Permission).HasMaxLength(100);
    }
}
