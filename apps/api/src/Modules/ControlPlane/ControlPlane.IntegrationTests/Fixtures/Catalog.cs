using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Fixtures;

// Writes catalog rows directly, the way the tenant creation flow will once it exists.
internal static class Catalog
{
    public static async Task<Tenant> AddTenantAsync(IServiceProvider services, TenantStatus status = TenantStatus.Active, string? slug = null)
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "Acme Ltd", slug ?? Unique.Slug()).Value;
        if (status is TenantStatus.Active or TenantStatus.Suspended)
        {
            tenant.Activate();
        }

        if (status == TenantStatus.Suspended)
        {
            tenant.Suspend();
        }

        await SaveAsync(services, catalog => catalog.Tenants.Add(tenant));
        return tenant;
    }

    public static async Task<User> AddUserAsync(IServiceProvider services, string? externalId = null)
    {
        var user = new User(Guid.CreateVersion7(), externalId ?? Unique.ExternalId());
        await SaveAsync(services, catalog => catalog.Users.Add(user));
        return user;
    }

    public static Task AddMemberAsync(IServiceProvider services, Tenant tenant, User user, Role? role = null) =>
        SaveInTenantAsync(services, tenant.Id, catalog => catalog.Memberships.Add(new Membership(tenant.Id, user.Id, (role ?? BuiltInRoles.Member).Id)));

    public static async Task<User> AddMemberAsync(IServiceProvider services, Tenant tenant, Role role)
    {
        var user = await AddUserAsync(services);
        await AddMemberAsync(services, tenant, user, role);
        return user;
    }

    private static async Task SaveAsync(IServiceProvider services, Action<ControlPlaneDbContext> change)
    {
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        change(catalog);
        await catalog.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A membership belongs to a tenant, so it is written in a transaction that declares its tenant.
    private static async Task SaveInTenantAsync(IServiceProvider services, Guid tenantId, Action<ControlPlaneDbContext> change)
    {
        await using var scope = services.CreateAsyncScope();
        var catalog = InTenant.Catalog(scope, tenantId);
        await using var transaction = await catalog.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        change(catalog);
        await catalog.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}

// The tests share one database, so each one works with tenants, users and email addresses of its own. The values never matter,
// only that they differ.
internal static class Unique
{
    public static string Slug() => $"tenant-{Guid.NewGuid():N}"[..20];

    public static string ExternalId() => $"user_{Guid.NewGuid():N}";

    public static string Email() => $"{Guid.NewGuid():N}@example.com";
}
