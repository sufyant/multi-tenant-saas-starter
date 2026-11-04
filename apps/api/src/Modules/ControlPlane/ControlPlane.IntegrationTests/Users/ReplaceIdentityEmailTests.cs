using ControlPlane.Application.Users;
using ControlPlane.Application.Users.ReplaceIdentityEmail;
using ControlPlane.Application.Users.SendIdentityEmail;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Users;

// A7: the identity provider takes the address that our record keeps when the step runs, not one the message carries, so two changes that
// reach it out of order still end on our record's address. The step runs the way Wolverine runs it: in a transaction that declares no
// tenant; the call to the provider is a step of its own.
public sealed class ReplaceIdentityEmailTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReplaceIdentityEmail_ALiveUser_SendsTheAddressOurRecordKeepsNow()
    {
        var user = await TestData.AddUserAsync(database.Services);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.users SET email = 'ada-{user.Id:N}@now.example' WHERE id = '{user.Id}'");

        var send = await ReplaceAsync(new ReplaceIdentityEmailCommand(user.Id, $"ada-{user.Id:N}@before.example"));

        send.ShouldBe(new SendIdentityEmailCommand(user.Id, user.ExternalId, $"ada-{user.Id:N}@now.example"));
    }

    // The provider got the address our record keeps, so the chain ends.
    [Fact]
    public async Task ReplaceIdentityEmail_TheProviderGotTheAddressOurRecordKeeps_SendsNothing()
    {
        var user = await TestData.AddUserAsync(database.Services);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.users SET email = 'ada-{user.Id:N}@now.example' WHERE id = '{user.Id}'");

        var send = await ReplaceAsync(new ReplaceIdentityEmailCommand(user.Id, $"ada-{user.Id:N}@now.example"));

        send.ShouldBeNull();
    }

    // The identity provider deletes its user with ours (A8), so a deleted user's change sends nothing.
    [Fact]
    public async Task ReplaceIdentityEmail_AUserDeletedMeanwhile_SendsNothing()
    {
        var user = await TestData.AddUserAsync(database.Services);
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.users SET deleted_at = now() WHERE id = '{user.Id}'");

        var send = await ReplaceAsync(new ReplaceIdentityEmailCommand(user.Id, null));

        send.ShouldBeNull();
    }

    private async Task<SendIdentityEmailCommand?> ReplaceAsync(ReplaceIdentityEmailCommand command)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlane.Infrastructure.Persistence.ControlPlaneDbContext>();
        await using var transaction = await controlPlane.Database.BeginTransactionAsync(Cancellation);
        var send = await ReplaceIdentityEmailHandler.HandleAsync(command, scope.ServiceProvider.GetRequiredService<IUserRepository>(), Cancellation);
        await transaction.CommitAsync(Cancellation);
        return send;
    }
}
