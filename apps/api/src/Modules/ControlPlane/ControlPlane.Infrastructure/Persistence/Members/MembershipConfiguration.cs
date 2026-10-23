using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Members;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> membership)
    {
        membership.HasKey(m => new { m.TenantId, m.UserId });
        membership.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId);
        membership.HasOne<User>().WithMany().HasForeignKey(m => m.UserId);
        // A2: a role of the membership's own tenant. A foreign key check bypasses row level security, so the key names the tenant too.
        membership.HasOne<Role>().WithMany().HasForeignKey(m => new { m.TenantId, m.RoleId }).HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
