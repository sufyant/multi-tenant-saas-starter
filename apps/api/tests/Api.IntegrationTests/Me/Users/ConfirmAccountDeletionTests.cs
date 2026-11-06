using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// POST /v1/me/account-deletion and .../confirm: the person deletes their own account, at once, after a fresh code to their address. The
// deletion takes the steps of a system admin's (docs/flows/user-deletion.md), by the user, and deletes the user's files too. It is refused
// for the last Owner of a tenant, whose tenants the answer names, and for a system admin.
public sealed class ConfirmAccountDeletionTests(Database database) : IAsyncLifetime
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

    // The deletion is the one of a system admin, by the user: the memberships go, the address and the names are erased, the identity
    // provider deletes its user, the file store the user's prefix, and the address the user had is told so (GDPR, Article 12(3)).
    [Fact]
    public async Task ConfirmAccountDeletion_TheRightCode_DeletesTheUserAndTheirFilesAndTellsTheirAddress()
    {
        var email = $"ada-{Guid.NewGuid():N}@example.com";
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email, name: "Ada Lovelace"));
        var userId = await _testData.UserIdOfAsync(user);
        await DataExportRequests.ReadyAsync(_api, user);
        await ObjectStorage.PutAsync($"{FileKeys.User(userId)}avatar", "a later module's file");
        await _api.WaitingForMessagesAsync(() => AccountRequests.RequestAccountDeletionAsync(_api, user));

        var deleted = await _api.WaitingForMessagesAsync(() => AccountRequests.ConfirmAccountDeletionAsync(_api, user, AccountRequests.CodeSentTo(_api, email)));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.UserOfAsync(user)).ShouldBe(new StoredUser(null, null, Deleted: true));
        (await _testData.StoredMembershipCountOfAsync(user)).ShouldBe(0);
        _api.Identity.Deleted.ShouldBe([user]);
        (await ObjectStorage.KeysAsync(FileKeys.User(userId))).ShouldBeEmpty();
        (await ObjectStorage.KeysAsync(FileKeys.UserExports(userId))).ShouldBeEmpty();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.stored_files WHERE user_id = '{userId}'")).ShouldBe(0);
        _api.Email.Sent.Single(sent => sent.IdempotencyKey == $"account-deleted/{userId}").To.ShouldBe(email);
        (await _audit.PlatformLogAsync(userId)).ShouldEndWith($"user.deleted|User|{userId}|User|{userId}|-");
    }

    [Fact]
    public async Task ConfirmAccountDeletion_AWrongCode_IsABadRequestAndDeletesNothing()
    {
        var email = $"ada-{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email, name: "Ada Lovelace");
        await _api.WaitingForMessagesAsync(() => AccountRequests.RequestAccountDeletionAsync(_api, user));

        var deleted = await AccountRequests.ConfirmAccountDeletionAsync(_api, user, "no-code");

        (deleted.StatusCode, await AccountRequests.CodeOfAsync(deleted)).ShouldBe((HttpStatusCode.BadRequest, "verification_code.invalid"));
        (await _testData.UserOfAsync(user)).Deleted.ShouldBeFalse();
        _api.Identity.Deleted.ShouldBeEmpty();
    }

    // A code of a change of address does not delete the account: each purpose has its own codes.
    [Fact]
    public async Task ConfirmAccountDeletion_ACodeOfAChangeOfAddress_IsRefused()
    {
        var user = await _testData.AddUserAsync(email: $"ada-{Guid.NewGuid():N}@example.com");
        var newEmail = $"a{Guid.NewGuid():N}@new.example";
        await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, newEmail));

        var deleted = await AccountRequests.ConfirmAccountDeletionAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail));

        (deleted.StatusCode, await AccountRequests.CodeOfAsync(deleted)).ShouldBe((HttpStatusCode.BadRequest, "verification_code.invalid"));
    }
}
