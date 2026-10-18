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

namespace ControlPlane.IntegrationTests.Tenants.TenantCreation;

// The steps of the tenant creation saga, each run the way Wolverine runs it: a step that writes the catalog in the new tenant's
// transaction. Each step and each compensation may run twice (S7).
public sealed class TenantCreationTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTenant_NewTenant_WritesTheTenantTheOwnersInvitationAndTheSaga()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();
        var slug = Unique.Slug();
        var owner = Unique.Email();

        var started = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, slug, owner);

        started.Result.Value.ShouldBe(new TenantView(tenantId, "Acme Ltd", slug, "Provisioning", null, null));
        var activate = started.Activate.ShouldNotBeNull().Message;
        var invitationId = await ScalarAsync<Guid>($"SELECT id FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");
        activate.ShouldSatisfyAllConditions(
            step => step.TenantId.ShouldBe(tenantId),
            step => step.InvitationId.ShouldBe(invitationId),
            step => step.AcceptLink.GetLeftPart(UriPartial.Path).ShouldBe(Database.AcceptUrl),
            step => Handlers.TenantIdOf(Handlers.CodeOf(step.AcceptLink)).ShouldBe(tenantId));
        started.Timeout.ShouldBe(new ActivationTimedOut(tenantId, TimeSpan.FromMinutes(10)));
        (await StatusOfAsync(tenantId)).ShouldBe("Provisioning");
        (await ScalarAsync<string>($"SELECT state FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}' AND tenant_id = '{tenantId}'"))
            .ShouldBe("Activating");
        (await ScalarAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.invitations JOIN control_plane.roles ON roles.id = invitations.role_id
            WHERE invitations.id = '{invitationId}' AND invitations.email = '{owner}' AND invitations.status = 'Pending'
              AND roles.built_in = 'Owner' AND invitations.invited_by = '{admin.Id}'
            """)).ShouldBe(1);
    }

    // The secret is born with the invitation and travels only in the accept link; the catalog keeps its SHA-256 hash.
    [Fact]
    public async Task CreateTenant_NewTenant_StoresOnlyTheHashOfTheSecretTheAcceptLinkCarries()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();

        var started = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), Unique.Email());

        var secret = Handlers.SecretOf(Handlers.CodeOf(started.Activate.ShouldNotBeNull().Message.AcceptLink));
        (await ScalarAsync<string>($"SELECT secret_hash FROM control_plane.invitations WHERE tenant_id = '{tenantId}'")).ShouldBe(Sha256(secret));
    }

    // The steps it starts carry the saga's id on their envelopes, so the fault of one that fails for good finds the saga.
    [Fact]
    public async Task CreateTenant_NewTenant_SendsTheActivationWithTheSagaId()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();

        var started = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), Unique.Email());

        started.Activate.ShouldNotBeNull().Options.SagaId.ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task CreateTenant_SlugTaken_IsRefusedAndStartsNothing()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var existing = await Catalog.AddTenantAsync(database.Services);

        var started = await Handlers.CreateTenantAsync(database.Services, Guid.CreateVersion7(), admin.ExternalId, existing.Slug, Unique.Email());

        started.Result.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSlugTaken);
        started.Saga.ShouldBeNull();
        started.Activate.ShouldBeNull();
        started.Timeout.ShouldBeNull();
    }

    // Both find the slug free, and the slug's unique index lets only one of them have it: the other is a conflict, not a failure.
    [Fact]
    public async Task CreateTenant_TwoWithOneSlugAtOnce_CreatesOneTenantAndRefusesTheOther()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var slug = Unique.Slug();
        await using var firstScope = database.Services.CreateAsyncScope();
        await using var secondScope = database.Services.CreateAsyncScope();
        var (firstTransaction, _) = await BeginAsync(firstScope, Guid.CreateVersion7());
        var (_, secondProcessId) = await BeginAsync(secondScope, Guid.CreateVersion7());
        (await StartAsync(firstScope, admin.ExternalId, slug)).IsSuccess.ShouldBeTrue();

        var second = StartAsync(secondScope, admin.ExternalId, slug);
        await WaitUntilBlockedAsync(secondProcessId);
        await firstTransaction.CommitAsync(Cancellation);

        (await second).Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSlugTaken);
        (await ScalarAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(1);
    }

    // Step 3 starts with the activation: the identity provider lets the owner sign up, until the invitation expires.
    [Fact]
    public async Task ActivateTenant_Provisioning_ActivatesItAnnouncesTheTenantAndOpensTheOwnersSignUp()
    {
        var admin = await Catalog.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();
        var owner = Unique.Email();
        var started = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), owner);
        var invitationId = started.Activate.ShouldNotBeNull().Message.InvitationId;
        var link = AcceptLink();

        var (completed, activated, signUp) = await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, link));

        (await StatusOfAsync(tenantId)).ShouldBe("Active");
        completed.ShouldBe(new ActivateTenantReply(tenantId));
        activated.ShouldNotBeNull().ShouldSatisfyAllConditions(
            announced => announced.TenantId.ShouldBe(tenantId),
            announced => announced.Name.ShouldBe("Acme Ltd"),
            announced => announced.CreatedBy.ShouldBe(admin.Id),
            announced => announced.EventId.ShouldNotBe(Guid.Empty));
        signUp.ShouldBe(new OpenOwnerSignUpCommand(
            tenantId,
            invitationId,
            owner,
            "Acme Ltd",
            link,
            await ScalarAsync<DateTime>($"SELECT expires_at FROM control_plane.invitations WHERE id = '{invitationId}'")));
    }

    [Fact]
    public async Task ActivateTenant_Repeated_AnnouncesNothingAgain()
    {
        var (tenantId, invitationId) = await StartAsync();
        var activate = new ActivateTenantCommand(tenantId, invitationId, AcceptLink());
        await Handlers.ActivateAsync(database.Services, tenantId, activate);

        var (completed, activated, ready) = await Handlers.ActivateAsync(database.Services, tenantId, activate);

        completed.ShouldBeNull();
        activated.ShouldBeNull();
        ready.ShouldBeNull();
    }

    [Fact]
    public async Task CancelTenant_Provisioning_CancelsTheTenantWithItsReasonAndTheInvitation()
    {
        var (tenantId, invitationId) = await StartAsync();

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        cancelled.ShouldBe(new CancelTenantReply(tenantId));
        (await StatusOfAsync(tenantId)).ShouldBe("Cancelled");
        (await ScalarAsync<string>($"SELECT cancellation_reason FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBe("activation_failed");
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Cancelled");
    }

    [Fact]
    public async Task CancelTenant_Repeated_KeepsTheFirstReason()
    {
        var (tenantId, invitationId) = await StartAsync();
        await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "another_reason"));

        cancelled.ShouldBe(new CancelTenantReply(tenantId));
        (await ScalarAsync<string>($"SELECT cancellation_reason FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBe("activation_failed");
    }

    // The activation is the pivot: an active tenant is never cancelled. The compensation changes nothing and reports nothing, so the
    // saga's cancellation timeout leaves the tenant creation to a person.
    [Fact]
    public async Task CancelTenant_Active_ChangesNothingAndReportsNoCancellation()
    {
        var (tenantId, invitationId) = await StartAsync();
        await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, AcceptLink()));

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        cancelled.ShouldBeNull();
        (await StatusOfAsync(tenantId)).ShouldBe("Active");
        (await ScalarAsync<bool>($"SELECT cancellation_reason IS NULL FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBeTrue();
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Pending");
    }

    // A suspended tenant is past the pivot too: a late compensation leaves it and its owner's invitation as they are.
    [Fact]
    public async Task CancelTenant_Suspended_ChangesNothingAndReportsNoCancellation()
    {
        var (tenantId, invitationId) = await StartAsync();
        await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, AcceptLink()));
        await ScalarAsync<object>($"UPDATE control_plane.tenants SET status = 'Suspended' WHERE id = '{tenantId}'");

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        cancelled.ShouldBeNull();
        (await StatusOfAsync(tenantId)).ShouldBe("Suspended");
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Pending");
    }

    [Fact]
    public async Task ActivateTenant_Cancelled_AnnouncesNothing()
    {
        var (tenantId, invitationId) = await StartAsync();
        await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        var (completed, activated, ready) = await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, AcceptLink()));

        completed.ShouldBeNull();
        activated.ShouldBeNull();
        ready.ShouldBeNull();
        (await StatusOfAsync(tenantId)).ShouldBe("Cancelled");
    }

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

    // The transaction Wolverine would begin for the tenant creation, and the database session it runs in.
    private static async Task<(IDbContextTransaction Transaction, int ProcessId)> BeginAsync(AsyncServiceScope scope, Guid tenantId)
    {
        var catalog = InTenant.Catalog(scope, tenantId);
        var transaction = await catalog.Database.BeginTransactionAsync(Cancellation);
        return (transaction, ((NpgsqlConnection)catalog.Database.GetDbConnection()).ProcessID);
    }

    private static async Task<SharedKernel.Result<TenantView>> StartAsync(AsyncServiceScope scope, string adminId, string slug) =>
        (await CreateTenantHandler.HandleAsync(
            new CreateTenantCommand(adminId, "Acme Ltd", slug, Unique.Email(), null),
            scope.ServiceProvider.GetRequiredService<ITenantRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<Application.Invitations.InvitationOptions>(),
            scope.ServiceProvider.GetRequiredService<TenantCreationOptions>(),
            TimeProvider.System,
            Cancellation)).Item1;

    // The second tenant creation must be waiting on the first one's insert before the first commits, or the test would prove nothing.
    private async Task WaitUntilBlockedAsync(int processId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application));
        await connection.OpenAsync(timeout.Token);
        await using var blocked = new NpgsqlCommand($"SELECT cardinality(pg_blocking_pids({processId})) > 0", connection);
        while (!(bool)(await blocked.ExecuteScalarAsync(timeout.Token))!)
        {
            timeout.Token.ThrowIfCancellationRequested();
        }
    }

    private Task<string> StatusOfAsync(Guid tenantId) => ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenantId}'");

    private static string Sha256(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private Task<T> ScalarAsync<T>(string sql) => database.ScalarAsSuperuserAsync<T>(sql);
}
