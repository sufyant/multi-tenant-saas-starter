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

        // A3: a custom role's permissions are its rows of role_permissions, a part of the role, which go with it. The key names the tenant
        // too, so a row never names another tenant's role: a foreign key check bypasses row level security (A2).
        role.HasMany(r => r.Grants)
            .WithOne()
            .HasForeignKey(g => new { g.TenantId, g.RoleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Cascade);
        role.Navigation(r => r.Grants).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A3: a role's name is unique in its tenant without regard to case, built-in names included, so no custom role is called "Owner".
        // The command checks it with lower() under the tenant's lock; the unique index ix_roles_tenant_id_lower_name, on the tenant and
        // lower(name), is the second guard (K6b). EF Core cannot model an index on an expression, so the migration AddRolePermissions
        // writes it in SQL, and the model does not hold it.

        // K5: the role list searches the name with a trigram index and sorts by it with a B-tree index.
        role.HasIndex(r => r.Name);
        role.HasIndex(r => r.Name, "ix_roles_name_trgm").HasDatabaseName("ix_roles_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");

        // A2: memberships and invitations name a role with its tenant, so the database refuses a role of another tenant.
        role.HasAlternateKey(r => new { r.TenantId, r.Id });

        // A2: a tenant has one role of each built-in kind; a custom role has none (A3). A built-in role's permissions come from code, so it
        // has no rows of role_permissions.
        role.HasIndex(r => new { r.TenantId, r.BuiltIn }).IsUnique().HasFilter("built_in IS NOT NULL");
        role.ToTable(table => table.HasCheckConstraint("ck_roles_built_in", "built_in IN ('Owner', 'Admin', 'Member')"));
    }
}
