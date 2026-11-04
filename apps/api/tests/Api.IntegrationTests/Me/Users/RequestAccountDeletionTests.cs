using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// POST /v1/me/account-deletion: the person asks for the code that deletes their own account. The deletion's check runs first, so the
// screen says at once why it cannot run: the last Owner of a tenant gets the tenants named, and a system admin is refused.
public sealed class RequestAccountDeletionTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The code goes to the person's own address, and the request records user.deletion_requested by the user.
    [Fact]
    public async Task RequestAccountDeletion_AMember_SendsACodeToTheirAddressAndRecordsTheRequest()
    {
        var email = $"ada-{Guid.NewGuid():N}@example.com";
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));
        var userId = await _testData.UserIdOfAsync(user);

        var requested = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestAccountDeletionAsync(_api, user));

        requested.StatusCode.ShouldBe(HttpStatusCode.Created);
        (_api.Email.Sent.ShouldHaveSingleItem().To, AccountRequests.CodeSentTo(_api, email).Length).ShouldBe((email, 6));
        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.deletion_requested|User|{userId}|User|{userId}|-");
    }

    // "Make someone else an Owner, or close the organisation first": the answer names the tenants, and no code goes out.
    [Fact]
    public async Task RequestAccountDeletion_TheLastOwnerOfATenant_IsAConflictNamingTheTenant()
    {
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.SummarizeAsync(tenant.Id);

        var requested = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestAccountDeletionAsync(_api, user));

        requested.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (problem.GetProperty("code").GetString(), problem.GetProperty("tenantIds")[0].GetGuid()).ShouldBe(("user.last_owner", tenant.Id));
        _api.Email.Sent.ShouldBeEmpty();
    }

    // A system admin's rights go first.
    [Fact]
    public async Task RequestAccountDeletion_ASystemAdmin_IsAConflict()
    {
        var admin = await _testData.AddSystemAdminAsync(email: $"admin-{Guid.NewGuid():N}@example.com");

        var requested = await AccountRequests.RequestAccountDeletionAsync(_api, admin);

        (requested.StatusCode, await AccountRequests.CodeOfAsync(requested)).ShouldBe((HttpStatusCode.Conflict, "user.is_system_admin"));
    }
}
