using ControlPlane.Application.Invitations.RecordSignUp;
using ControlPlane.Contracts.Invitations;
using Npgsql;
using Tenancy;
using Database = ControlPlane.IntegrationTests.Fixtures.Database;

namespace ControlPlane.IntegrationTests.Invitations;

// RecordSignUpCommand, run the way Wolverine runs it: the identity provider's answer is recorded on the invitation in the invitation's
// tenant. It locks the tenant row first, then the invitation (K6m).
public sealed class RecordSignUpTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // K6m: the invitation belongs to the tenant aggregate, so the record locks the tenant row first. While another transaction holds
    // the tenant row, the record waits, and then hands the link to Notifications.
    [Fact]
    public async Task RecordSignUp_WhileAnotherTransactionHoldsTheTenant_WaitsForIt()
    {
        var (tenantId, _, signUp) = await Handlers.CreateActiveTenantAsync(database.Services, Unique.Email());
        await using var other = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application));
        await other.OpenAsync(Cancellation);
        await using var holding = await other.BeginTransactionAsync(Cancellation);
        await using var lockTenant = new NpgsqlCommand($"SELECT 1 FROM control_plane.tenants WHERE id = '{tenantId}' FOR UPDATE", other, holding);
        await lockTenant.ExecuteNonQueryAsync(Cancellation);

        var recording = Handlers.RecordSignUpAsync(
            database.Services,
            tenantId,
            new RecordSignUpCommand(tenantId, signUp.InvitationId, signUp.Email, signUp.TenantName, signUp.AcceptLink, "inv_1", signUp.ExpiresAt));
        var waited = await WaitsForAsync(other.ProcessID, recording);
        await holding.CommitAsync(Cancellation);
        var messages = await recording;

        waited.ShouldBeTrue();
        messages.OfType<InvitationReadyEvent>().ShouldHaveSingleItem().InvitationId.ShouldBe(signUp.InvitationId);
    }

    // True once another session waits for a lock that the holder's session holds; false when the work finishes first, so it never
    // waited.
    private async Task<bool> WaitsForAsync(int holder, Task work)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!work.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))") > 0)
            {
                return true;
            }

            await Task.Delay(20, Cancellation);
        }

        return false;
    }
}
