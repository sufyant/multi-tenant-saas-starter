using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Contracts.Invitations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Contracts.Invitations;
using Npgsql;
using SharedKernel;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Platform;

// The tenant creation process through the real host, apart from each run of it, which CreateTenantTests follows: the fault of a step
// that fails for good, an event that arrives twice, and two messages that change the saga's record at once. Only Clerk and the email
// service are fakes.
public sealed class TenantCreationTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), configureServices: services => services.AddFakeLogging());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // Wolverine publishes the fault of a message that went to the dead letter queue with the message's tenant and saga id, so the
    // saga finds its record under row level security.
    [Fact]
    public async Task FailForGood_AStepOfTheTenantCreation_TheFaultCarriesItsTenantAndSagaId()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var slug = Slug();
        await database.RefuseAsync("tenants", "UPDATE", $"NEW.slug = '{slug}' AND NEW.status = 'Active'");

        HttpResponseMessage created = null!;
        var messages = await _api.TrackMessagesAsync(async () => created = await CreateTenantAsync(admin, slug, Email()));

        var tenantId = (await IdOfAsync(created)).ToString();
        var fault = messages.Executed.SingleEnvelope<Fault<ActivateTenantCommand>>();
        fault.TenantId.ShouldBe(tenantId);
        fault.SagaId.ShouldBe(tenantId);
        ((Fault<ActivateTenantCommand>)fault.Message!).TenantId.ShouldBe(tenantId);
    }

    // O4: the email is sent under the invitation's idempotency key, so the event that arrives again sends no second email.
    [Fact]
    public async Task SendInvitation_TheEventArrivesTwice_SendsOneEmail()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var owner = Email();
        var created = await _api.WaitingForMessagesAsync(() => CreateTenantAsync(admin, Slug(), owner));
        var tenantId = await IdOfAsync(created);
        var invitationId = await InvitationIdAsync(tenantId);
        var again = new OwnerInvitationReadyEvent(
            Guid.CreateVersion7(), DateTimeOffset.UtcNow, tenantId, invitationId, owner, "Acme Ltd", _api.Email.LinkSentTo(owner), Language.En, TimeSpan.FromDays(7));

        await _api.WaitingForMessagesAsync(async () =>
        {
            await Bus().PublishAsync(again, new DeliveryOptions { TenantId = tenantId.ToString(), SagaId = tenantId.ToString() });
            return true;
        });

        _api.Email.Attempts.Where(attempt => attempt.Email.To == owner).Select(attempt => attempt.Email.IdempotencyKey)
            .ShouldBe([$"invite/{invitationId}", $"invite/{invitationId}"]);
        _api.Email.Sent.Where(email => email.To == owner).ShouldHaveSingleItem();
        (await SagaStateAsync(tenantId)).ShouldBe("Completed");
    }

    // W6, spike T9: the activation and the email's report reach the saga at the same moment, and both read its record before either
    // writes. The report writes first and completes the tenant creation. The activation's write then fails on the version check, and its
    // retry finds the tenant creation completed. Without the check, the activation would put the saga back to waiting for an email that
    // was already sent.
    [Fact]
    public async Task UpdateTenantCreationSaga_TwoMessagesAtOnce_KeepsBothChanges()
    {
        var tenantId = Guid.CreateVersion7();
        var invitationId = Guid.CreateVersion7();
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.tenant_creation_sagas (id, state, invitation_id, invitation_email_timeout, cancellation_timeout, version)
            VALUES ('{tenantId}', 'Activating', '{invitationId}', interval '2 hours', interval '10 minutes', 0)
            """);
        var now = DateTimeOffset.UtcNow;
        var emailSent = new Notifications.Contracts.Invitations.SendInvitationEmailReply(Guid.CreateVersion7(), now, tenantId, invitationId);
        var activated = new ActivateTenantReply(tenantId);
        await using var hold = await HoldSagaAsync(tenantId);

        var messages = await _api.TrackMessagesAsync(async () =>
        {
            await Bus().PublishAsync(emailSent, new DeliveryOptions { TenantId = tenantId.ToString() });
            await hold.WaitForWritersAsync(1);
            await Bus().PublishAsync(activated, new DeliveryOptions { TenantId = tenantId.ToString() });
            await hold.WaitForWritersAsync(2);
            await hold.ReleaseAsync();
        });

        (await SagaStateAsync(tenantId)).ShouldBe("Completed");
        messages.MovedToErrorQueue.Envelopes().ShouldBeEmpty();
        _api.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(record => record.Exception is SagaConcurrencyException);
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage created)
    {
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    private static string Slug() => $"tenant-{Guid.NewGuid():N}"[..20];

    private static string Email() => $"{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> CreateTenantAsync(string admin, string slug, string ownerEmail) =>
        _api.CreateClient(admin, secondFactor: true)
            .CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail });

    private IMessageBus Bus() => _api.Services.GetRequiredService<IHost>().MessageBus();

    private async Task<Guid> InvitationIdAsync(Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<string?> SagaStateAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT state FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}'");

    // Locks the saga's record in a transaction of its own, so the handlers that read it all wait at their write.
    private async Task<SagaHold> HoldSagaAsync(Guid tenantId)
    {
        var connection = new NpgsqlConnection(database.SuperuserConnectionString);
        await connection.OpenAsync(Cancellation);
        var transaction = await connection.BeginTransactionAsync(Cancellation);
        await using var lockRow = new NpgsqlCommand($"SELECT 1 FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}' FOR UPDATE", connection, transaction);
        await lockRow.ExecuteNonQueryAsync(Cancellation);

        return new SagaHold(database, connection, transaction);
    }

    private sealed class SagaHold(Database database, NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        // Waits until the given number of sessions wait to write the saga's record. Only the first waits on this transaction itself;
        // PostgreSQL queues the others behind it, and lets them write in that order.
        public async Task WaitForWritersAsync(int writers)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await using var observer = new NpgsqlConnection(database.SuperuserConnectionString);
            await observer.OpenAsync(timeout.Token);
            await using var waiting = new NpgsqlCommand(
                $"""
                SELECT count(*) FROM pg_stat_activity
                WHERE pid <> {connection.ProcessID} AND wait_event_type = 'Lock' AND query LIKE '%UPDATE control_plane.tenant_creation_sagas%'
                """,
                observer);
            while ((long)(await waiting.ExecuteScalarAsync(timeout.Token))! < writers)
            {
                timeout.Token.ThrowIfCancellationRequested();
            }
        }

        public Task ReleaseAsync() => transaction.RollbackAsync(Cancellation);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
