using ControlPlane.Domain.Terms;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Terms;

// A user's acceptance of a terms version. It belongs to a user, above any tenant, and the platform counts and lists every version's
// acceptances, so it is outside row level security, as the users are (R6). The version's and the user's keys restrict their deletion (R16):
// an acceptance is a proof, and both rows stay.
internal sealed class TermsAcceptanceConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> acceptance)
    {
        acceptance.HasKey(a => new { a.VersionId, a.UserId });
        acceptance.HasOne<TermsVersion>().WithMany().HasForeignKey(a => a.VersionId).OnDelete(DeleteBehavior.Restrict);
        acceptance.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

        // The gate reads one user's acceptances; the platform's list of a version's people pages by the moment, then the user (K5).
        acceptance.HasIndex(a => a.UserId);
        acceptance.HasIndex(a => new { a.VersionId, a.AcceptedAt, a.UserId });
    }
}
