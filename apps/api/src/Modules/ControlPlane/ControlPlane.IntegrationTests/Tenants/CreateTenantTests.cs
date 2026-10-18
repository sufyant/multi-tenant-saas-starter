using System.Security.Cryptography;
using System.Text;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CancelTenant;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users;
using ControlPlane.Domain;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenancy;
using Database = ControlPlane.IntegrationTests.Fixtures.Database;

namespace ControlPlane.IntegrationTests.Tenants;

// CreateTenantCommand, the first step of the tenant creation saga, run the way Wolverine runs it: it writes the tenant, the Owner's
// invitation and the saga's record in the new tenant's transaction. The slug's unique index decides between two creations at once.
public sealed class CreateTenantTests(Database database)
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

    private static Uri AcceptLink() => new($"{Database.AcceptUrl}?code={Guid.CreateVersion7()}.secret");

    // The transaction Wolverine would begin for the tenant creation, and the database session it runs in.
    private static async Task<(IDbContextTransaction Transaction, int ProcessId)> BeginAsync(AsyncServiceScope scope, Guid tenantId)
    {
        var catalog = InTenant.Catalog(scope, tenantId);
        var transaction = await catalog.Database.BeginTransactionAsync(Cancellation);
        return (transaction, ((NpgsqlConnection)catalog.Database.GetDbConnection()).ProcessID);
    }

    private static async Task<SharedKernel.Result<TenantView>> StartAsync(AsyncServiceScope scope, string actorId, string slug) =>
        (await CreateTenantHandler.HandleAsync(
            new CreateTenantCommand(actorId, "Acme Ltd", slug, Unique.Email(), null),
            scope.ServiceProvider.GetRequiredService<ITenantRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<Application.Invitations.InvitationOptions>(),
            scope.ServiceProvider.GetRequiredService<TenantCreationOptions>(),
            TimeProvider.System,
            Cancellation)).Item1;

    private static string Sha256(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

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

    private Task<T> ScalarAsync<T>(string sql) => database.ScalarAsSuperuserAsync<T>(sql);
}
