using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

internal sealed class SystemAdminConfiguration : IEntityTypeConfiguration<SystemAdmin>
{
    public void Configure(EntityTypeBuilder<SystemAdmin> admin)
    {
        admin.HasKey(a => a.UserId);
        // R16: a grant goes only through RevokeSystemAdmin, which records it (K3).
        admin.HasOne<User>().WithOne().HasForeignKey<SystemAdmin>(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        // The system admin who granted it; none when the platform did (docs/flows/system-admins.md).
        admin.HasOne<User>().WithMany().HasForeignKey(a => a.GrantedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
