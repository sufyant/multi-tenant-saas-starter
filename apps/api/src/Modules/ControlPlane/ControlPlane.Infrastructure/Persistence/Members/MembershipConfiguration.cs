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
        // R16: a membership goes only through a step of the code, which counts it (R12, R13) and records it (K3).
        membership.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        membership.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        // A2: a role of the membership's own tenant. A foreign key check bypasses row level security, so the key names the tenant too.
        membership.HasOne<Role>().WithMany().HasForeignKey(m => new { m.TenantId, m.RoleId }).HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
