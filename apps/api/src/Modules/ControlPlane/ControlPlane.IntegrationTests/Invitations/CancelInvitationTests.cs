using System.Security.Cryptography;
using System.Text;
using ControlPlane.Application.Invitations.CancelInvitation;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.CancelTenant;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users;
using ControlPlane.Domain;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;
using Tenancy;
using Database = ControlPlane.IntegrationTests.Fixtures.Database;

namespace ControlPlane.IntegrationTests.Invitations;

// CancelInvitationCommand, the saga's compensation of the Owner's invitation, run the way Wolverine runs a step of the tenant creation
// saga: it writes the catalog in the new tenant's transaction, and it may run twice (S7). It locks the tenant row first (K6).
public sealed class CancelInvitationTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CancelInvitation_Pending_CancelsItAndATenantStaysActive()
    {
        var (tenantId, invitationId) = await StartAsync();
        await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, AcceptLink()));

        await Handlers.CancelInvitationAsync(database.Services, tenantId, new CancelInvitationCommand(tenantId, invitationId));
        await Handlers.CancelInvitationAsync(database.Services, tenantId, new CancelInvitationCommand(tenantId, invitationId));

        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Cancelled");
        (await StatusOfAsync(tenantId)).ShouldBe("Active");
    }

    // K6: the invitation belongs to the tenant aggregate, so the compensation locks the tenant row first, then the invitation. While
    // another transaction holds the tenant row, the compensation waits.
    [Fact]
    public async Task CancelInvitation_WhileAnotherTransactionHoldsTheTenant_WaitsForIt()
    {
        var (tenantId, invitationId) = await StartAsync();
        await using var other = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application));
        await other.OpenAsync(Cancellation);
        await using var holding = await other.BeginTransactionAsync(Cancellation);
        await using var lockTenant = new NpgsqlCommand($"SELECT 1 FROM control_plane.tenants WHERE id = '{tenantId}' FOR UPDATE", other, holding);
        await lockTenant.ExecuteNonQueryAsync(Cancellation);

        var cancelling = Handlers.CancelInvitationAsync(database.Services, tenantId, new CancelInvitationCommand(tenantId, invitationId));
        var waited = await WaitsForAsync(other.ProcessID, cancelling);
        await holding.CommitAsync(Cancellation);
        await cancelling;

        waited.ShouldBeTrue();
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Cancelled");
    }

    // S7: the owner accepted before the cancel arrived, and the acceptance stands.
    [Fact]
    public async Task CancelInvitation_AlreadyAccepted_ChangesNothing()
    {
        var owner = Unique.Email();
        var userId = Unique.ExternalId();
        database.Identity.AddAccount(userId, owner);
        var (tenantId, _, ready) = await Handlers.CreateActiveTenantAsync(database.Services, owner);
        (await Handlers.AcceptAsync(database.Services, Handlers.CodeOf(ready.AcceptLink), userId)).IsSuccess.ShouldBeTrue();

        var withdrawal = await Handlers.CancelInvitationAsync(database.Services, tenantId, new CancelInvitationCommand(tenantId, ready.InvitationId));

        withdrawal.ShouldBeNull();
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{ready.InvitationId}'")).ShouldBe("Accepted");
        (await ScalarAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'")).ShouldBe(1);
    }

    // True once another session waits for a lock that the holder's session holds; false when the work finishes first, so it never
    // waited.
    private async Task<bool> WaitsForAsync(int holder, Task work)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!work.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (await ScalarAsync<long>($"SELECT count(*) FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))") > 0)
            {
                return true;
            }

            await Task.Delay(20, Cancellation);
        }

        return false;
    }

    private async Task<(Guid TenantId, Guid InvitationId)> StartAsync()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();
        var started = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), Unique.Email());

        return (tenantId, started.Activate.ShouldNotBeNull().Message.InvitationId);
    }

    private static Uri AcceptLink() => new($"{Database.AcceptUrl}?code={Guid.CreateVersion7()}.secret");

    private static async Task<SharedKernel.Result<TenantView>> StartAsync(AsyncServiceScope scope, string adminId, string slug) =>
        (await CreateTenantHandler.HandleAsync(
            new CreateTenantCommand(adminId, "Acme Ltd", slug, Unique.Email(), null),
            scope.ServiceProvider.GetRequiredService<ITenantRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<Application.Invitations.InvitationOptions>(),
            scope.ServiceProvider.GetRequiredService<TenantCreationOptions>(),
            TimeProvider.System,
            Cancellation)).Item1;

    private Task<string> StatusOfAsync(Guid tenantId) => ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<T> ScalarAsync<T>(string sql) => database.ScalarAsSuperuserAsync<T>(sql);
}
