using System.Security.Cryptography;
using System.Text;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CreateTenant;
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

// CreateTenantCommand, run the way Wolverine runs it: one transaction in the new tenant writes the active tenant, its summary, its roles
// and the Owner's invitation, and returns the audit event and the identity provider's sign-up for the outbox. The slug's unique index decides
// between two creations at once.
public sealed class CreateTenantTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTenant_NewTenant_WritesAnActiveTenantAndThePendingOwnerInvitation()
    {
        var admin = await TestData.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();
        var slug = Unique.Slug();
        var owner = Unique.Email();

        var created = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, slug, owner);

        created.Result.Value.ShouldBe(new TenantView(tenantId, "Acme Ltd", slug, "Active", null, null));
        (await StatusOfAsync(tenantId)).ShouldBe("Active");
        (await ScalarAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.invitations JOIN control_plane.roles ON roles.id = invitations.role_id
            WHERE invitations.tenant_id = '{tenantId}' AND invitations.email = '{owner}' AND invitations.status = 'Pending'
              AND roles.built_in = 'Owner' AND invitations.invited_by = '{admin.Id}'
            """)).ShouldBe(1);
        (await ScalarAsync<string>($"SELECT owner_invitation_email FROM control_plane.tenant_summaries WHERE tenant_id = '{tenantId}'")).ShouldBe(owner);
    }

    // A2: the creation writes the tenant's own Owner, Admin and Member roles in its transaction, and invites the Owner with its own
    // tenant's Owner role.
    [Fact]
    public async Task CreateTenant_NewTenant_WritesItsOwnRolesAndInvitesWithItsOwnOwnerRole()
    {
        var admin = await TestData.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();

        await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), Unique.Email());

        (await ScalarAsync<string>(
            $"SELECT string_agg(format('%s|%s', built_in, name), ', ' ORDER BY built_in) FROM control_plane.roles WHERE tenant_id = '{tenantId}'"))
            .ShouldBe("Admin|admin, Member|member, Owner|owner");
        (await ScalarAsync<string>(
            $"""
            SELECT roles.built_in FROM control_plane.invitations
            JOIN control_plane.roles ON roles.tenant_id = invitations.tenant_id AND roles.id = invitations.role_id
            WHERE invitations.tenant_id = '{tenantId}'
            """)).ShouldBe("Owner");
    }

    // The creation sends the owner's sign-up, with the accept link that carries the invitation's code, and the event Audit records.
    [Fact]
    public async Task CreateTenant_NewTenant_SendsTheSignUpAndTheCreationEvent()
    {
        var admin = await TestData.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();
        var owner = Unique.Email();

        var created = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), owner);

        var invitationId = await ScalarAsync<Guid>($"SELECT id FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");
        created.SignUp.ShouldNotBeNull().ShouldSatisfyAllConditions(
            signUp => signUp.TenantId.ShouldBe(tenantId),
            signUp => signUp.InvitationId.ShouldBe(invitationId),
            signUp => signUp.Email.ShouldBe(owner),
            signUp => signUp.TenantName.ShouldBe("Acme Ltd"),
            signUp => signUp.AcceptLink.GetLeftPart(UriPartial.Path).ShouldBe(Database.AcceptUrl),
            signUp => Handlers.TenantIdOf(Handlers.CodeOf(signUp.AcceptLink)).ShouldBe(tenantId));
        created.Created.ShouldNotBeNull().ShouldSatisfyAllConditions(
            announced => announced.TenantId.ShouldBe(tenantId),
            announced => announced.Name.ShouldBe("Acme Ltd"),
            announced => announced.CreatedBy.ShouldBe(admin.Id));
    }

    // The secret is born with the invitation and travels only in the accept link; ControlPlane keeps its SHA-256 hash.
    [Fact]
    public async Task CreateTenant_NewTenant_StoresOnlyTheHashOfTheSecretTheAcceptLinkCarries()
    {
        var admin = await TestData.AddUserAsync(database.Services);
        var tenantId = Guid.CreateVersion7();

        var created = await Handlers.CreateTenantAsync(database.Services, tenantId, admin.ExternalId, Unique.Slug(), Unique.Email());

        var secret = Handlers.SecretOf(Handlers.CodeOf(created.SignUp.ShouldNotBeNull().AcceptLink));
        (await ScalarAsync<string>($"SELECT secret_hash FROM control_plane.invitations WHERE tenant_id = '{tenantId}'")).ShouldBe(Sha256(secret));
    }

    [Fact]
    public async Task CreateTenant_SlugTaken_IsRefusedAndSendsNothing()
    {
        var admin = await TestData.AddUserAsync(database.Services);
        var existing = await TestData.AddTenantAsync(database.Services);

        var created = await Handlers.CreateTenantAsync(database.Services, Guid.CreateVersion7(), admin.ExternalId, existing.Slug, Unique.Email());

        created.Result.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSlugTaken);
        created.Created.ShouldBeNull();
        created.SignUp.ShouldBeNull();
    }

    [Fact]
    public async Task CreateTenant_TwoWithOneSlugAtOnce_CreatesOneTenantAndRefusesTheOther()
    {
        var admin = await TestData.AddUserAsync(database.Services);
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

    // The transaction Wolverine would begin for the tenant creation, and the database session it runs in.
    private static async Task<(IDbContextTransaction Transaction, int ProcessId)> BeginAsync(AsyncServiceScope scope, Guid tenantId)
    {
        var controlPlane = InTenant.ControlPlane(scope, tenantId);
        var transaction = await controlPlane.Database.BeginTransactionAsync(Cancellation);
        return (transaction, ((NpgsqlConnection)controlPlane.Database.GetDbConnection()).ProcessID);
    }

    private static async Task<SharedKernel.Result<TenantView>> StartAsync(AsyncServiceScope scope, string actorId, string slug) =>
        (await CreateTenantHandler.HandleAsync(
            new CreateTenantCommand(actorId, "Acme Ltd", slug, Unique.Email(), null),
            scope.ServiceProvider.GetRequiredService<ITenantRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<Application.Invitations.InvitationOptions>(),
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
