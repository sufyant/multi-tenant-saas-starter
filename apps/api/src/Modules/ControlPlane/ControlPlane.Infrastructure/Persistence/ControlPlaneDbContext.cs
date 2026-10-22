using System.Text.Json;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Invitations;
using ControlPlane.Infrastructure.Persistence.Members;
using ControlPlane.Infrastructure.Persistence.Roles;
using ControlPlane.Infrastructure.Persistence.SystemAdmins;
using ControlPlane.Infrastructure.Persistence.Tenants;
using ControlPlane.Infrastructure.Persistence.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharedKernel;
using Tenancy;
using Wolverine;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The <c>control_plane</c> schema: the control plane's data above tenants (tenants and their summaries, users, roles, system admins), and the memberships,
/// invitations, which belong to a tenant and are under row level security.
/// </summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "control_plane";

    // RFC 5646, section 4.4.1: a buffer of 35 characters holds any language tag a program is likely to meet.
    internal const int LanguageTagMaxLength = 35;

    // A language is kept as its tag, the value it has in JSON ("en"), so a new member of the enum moves no stored value.
    internal static readonly ValueConverter<Language, string> LanguageTag = new(
        language => JsonSerializer.Serialize(language, JsonSerializerOptions.Default).Trim('"'),
        tag => JsonSerializer.Deserialize<Language>($"\"{tag}\"", JsonSerializerOptions.Default));

    internal DbSet<Tenant> Tenants => Set<Tenant>();

    internal DbSet<TenantSummary> TenantSummaries => Set<TenantSummary>();

    internal DbSet<User> Users => Set<User>();

    internal DbSet<Membership> Memberships => Set<Membership>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<Invitation> Invitations => Set<Invitation>();

    internal DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();

    /// <summary>
    /// The tenant of the message the handler serves (W2). Tenant-owned rows are reached only inside a tenant, so a read of them fails
    /// fast without one.
    /// </summary>
    internal Guid ActiveTenant =>
        MessageTenantId ?? throw new InvalidOperationException("Tenant-owned rows are reached only inside a tenant.");

    /// <summary>The active tenant's memberships. Row level security keeps them to the tenant; the query adds no filter of its own.</summary>
    internal IQueryable<Membership> TenantMemberships
    {
        get
        {
            _ = ActiveTenant;
            return Memberships;
        }
    }

    /// <summary>
    /// Whether the transaction saved a change to a membership or an invitation after the tenant's summary was last written (K6k). The
    /// transaction then does not commit (<see cref="TenantSummaryInterceptor"/>): <see cref="TenantRepository"/> writes the summary.
    /// </summary>
    internal bool SummaryOwed { get; private set; }

    /// <summary>The active tenant's invitations. Row level security keeps them to the tenant; the query adds no filter of its own.</summary>
    internal IQueryable<Invitation> TenantInvitations
    {
        get
        {
            _ = ActiveTenant;
            return Invitations;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OweSummary();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        OweSummary();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>The repository wrote the tenant's summary after the last change to a membership or an invitation.</summary>
    internal void SummaryWritten() => SummaryOwed = false;

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // K5: every field a list searches has a trigram index, which serves a "contains" match; every field it sorts by has a B-tree
        // index. pg_trgm is a trusted extension, so the migration account creates it with the database's CREATE privilege.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSummaryConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new InvitationConfiguration());
        modelBuilder.ApplyConfiguration(new SystemAdminConfiguration());
    }

    // Only a change inside a transaction is owed: the transaction is what commits it (W1). Entries() detects the changes first.
    private void OweSummary()
    {
        if (Database.CurrentTransaction is not null
            && ChangeTracker.Entries().Any(entry => entry.Entity is Membership or Invitation && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            SummaryOwed = true;
        }
    }
}
