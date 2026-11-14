using ControlPlane.Domain.TermsVersions;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

// A terms version belongs to no tenant: every tenant user reads the one in force, and the platform manages them (R6).
internal sealed class TermsVersionConfiguration : IEntityTypeConfiguration<TermsVersion>
{
    public void Configure(EntityTypeBuilder<TermsVersion> version)
    {
        version.Property(v => v.Id).ValueGeneratedNever();
        version.Property(v => v.Url).HasMaxLength(TermsVersion.UrlMaxLength);
        version.Property(v => v.Summary).HasMaxLength(TermsVersion.SummaryMaxLength);
        version.Ignore(v => v.Values);
        version.HasOne<User>().WithMany().HasForeignKey(v => v.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        version.HasOne<User>().WithMany().HasForeignKey(v => v.CancelledBy).OnDelete(DeleteBehavior.Restrict);

        // The timeline reads the versions by their day, then their creation (TermsTimeline).
        version.HasIndex(v => new { v.EffectiveDate, v.CreatedAt });

        // R15: a cancellation names its system admin, and a link is an https URL; the version checks the whole URL itself.
        version.ToTable(table => table.HasCheckConstraint("ck_terms_versions_cancellation", "(cancelled_at IS NULL) = (cancelled_by IS NULL)"));
        version.ToTable(table => table.HasCheckConstraint("ck_terms_versions_url_https", "url LIKE 'https://%'"));

        // R15: a version that sends its email has the moment it went, and only such a version has one.
        version.ToTable(table => table.HasCheckConstraint("ck_terms_versions_email_sent", "sends_email = (email_sent_at IS NOT NULL)"));
    }
}
