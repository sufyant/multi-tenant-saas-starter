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
        membership.HasOne<Role>().WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
