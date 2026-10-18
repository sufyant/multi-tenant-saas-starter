using ControlPlane.Domain.Roles;
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

        // Built-in roles are shared by every tenant; their permissions come from code, so their rows hold none.
        role.HasData(BuiltInRoles.All.Select(builtIn => new { builtIn.Id, builtIn.Name, builtIn.BuiltIn }));
    }
}
