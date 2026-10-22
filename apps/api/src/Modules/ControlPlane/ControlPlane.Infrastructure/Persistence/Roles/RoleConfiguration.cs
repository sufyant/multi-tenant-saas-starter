using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> role)
    {
        role.Property(r => r.Id).ValueGeneratedNever();
        role.Property(r => r.Name).HasMaxLength(Role.NameMaxLength);
        role.Property(r => r.BuiltIn).HasConversion<string>().HasMaxLength(20);
        role.Ignore(r => r.Permissions);
        role.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId);

        // A2: memberships and invitations name a role with its tenant, so the database refuses a role of another tenant.
        role.HasAlternateKey(r => new { r.TenantId, r.Id });

        // A2: a tenant has one role of each built-in kind; a custom role has none (A3). Their permissions come from code, so the rows hold
        // none.
        role.HasIndex(r => new { r.TenantId, r.BuiltIn }).IsUnique().HasFilter("built_in IS NOT NULL");
        role.ToTable(table => table.HasCheckConstraint("ck_roles_built_in", "built_in IN ('Owner', 'Admin', 'Member')"));
    }
}
