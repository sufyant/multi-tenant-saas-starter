using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Fixtures;

// Writes ControlPlane's rows directly, the way the tenant creation flow will once it exists.
internal static class TestData
{
    public static async Task<Tenant> AddTenantAsync(IServiceProvider services, TenantStatus status = TenantStatus.Active, string? slug = null)
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "Acme Ltd", slug ?? Unique.Slug(), DateTimeOffset.UtcNow).Value;
        if (status is TenantStatus.Active or TenantStatus.Suspended or TenantStatus.Closed)
        {
            tenant.Activate();
        }

        if (status == TenantStatus.Suspended)
        {
            tenant.Suspend("Unpaid invoices");
        }

        if (status == TenantStatus.Closed)
        {
            tenant.Close(DateTimeOffset.UtcNow, []);
        }

        // Every tenant has its summary from its creation on (R12).
        await SaveAsync(services, controlPlane =>
        {
            controlPlane.Tenants.Add(tenant);
            controlPlane.TenantSummaries.Add(new TenantSummary(tenant.Id));
        });
        return tenant;
    }

    public static async Task<User> AddUserAsync(IServiceProvider services, string? externalId = null)
    {
        var user = new User(Guid.CreateVersion7(), externalId ?? Unique.ExternalId());
        await SaveAsync(services, controlPlane => controlPlane.Users.Add(user));
        return user;
    }

    // The user's membership count and the tenant's summary follow, as the commands keep them: through the repository (K6k, R13).
    public static Task AddMemberAsync(IServiceProvider services, Tenant tenant, User user, Role? role = null) =>
        InTenant.RunAsync(services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            var locked = await tenants.FindTenantForUpdateAsync(TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("A member joins a tenant that exists.");
            await tenants.AddMembershipAsync(locked, user.Id, (role ?? BuiltInRoles.Member).Id, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        });

    public static async Task<User> AddMemberAsync(IServiceProvider services, Tenant tenant, Role role)
    {
        var user = await AddUserAsync(services);
        await AddMemberAsync(services, tenant, user, role);
        return user;
    }

    // The user's stored count of memberships and the tenant's summary, as the commands leave them (R13). Neither table belongs to a tenant.
    public static async Task<int> MembershipCountOfAsync(IServiceProvider services, User user)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>().Users
            .Where(found => found.Id == user.Id).Select(found => found.MembershipCount).SingleAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<string?> OwnerInvitationEmailOfAsync(IServiceProvider services, Tenant tenant)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>().TenantSummaries
            .Where(found => found.TenantId == tenant.Id).Select(found => found.OwnerInvitationEmail).SingleAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<int> MemberCountOfAsync(IServiceProvider services, Tenant tenant)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>().TenantSummaries
            .Where(found => found.TenantId == tenant.Id).Select(found => found.MemberCount).SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SaveAsync(IServiceProvider services, Action<ControlPlaneDbContext> change)
    {
        await using var scope = services.CreateAsyncScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        change(controlPlane);
        await controlPlane.SaveChangesAsync(TestContext.Current.CancellationToken);
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
