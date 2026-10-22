using ControlPlane.Application.Users;
using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Application.Users.DeleteUser;
using ControlPlane.Domain;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Users;

// The last step of a user's deletion, run the way Wolverine runs it: in a transaction that declares no tenant, and the user only to read
// the user's own memberships (R11).
public sealed class DeleteUserTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // A membership that appeared after the user left their tenants, such as an invitation accepted at the same moment, stops the
    // erasure: a deleted user is never a member (A8).
    [Fact]
    public async Task DeleteUser_UserStillAMember_IsAConflictAndErasesNothing()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, user);
        var admin = await TestData.AddUserAsync(database.Services);

        var (result, deleted, identity) = await DeleteAsync(new DeleteUserCommand(admin.ExternalId, user.Id));

        result.Error.Code.ShouldBe(ControlPlaneErrorCodes.UserHasMemberships);
        deleted.ShouldBeNull();
        identity.ShouldBeNull();
        (await database.ScalarAsSuperuserAsync<bool>($"SELECT deleted_at IS NULL FROM control_plane.users WHERE id = '{user.Id}'")).ShouldBeTrue();
    }

    private async Task<(SharedKernel.Result Result, ControlPlane.Contracts.Users.UserDeletedEvent? Deleted, DeleteIdentityAccountCommand? Identity)> DeleteAsync(DeleteUserCommand command)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlane.Infrastructure.Persistence.ControlPlaneDbContext>();
        await using var transaction = await controlPlane.Database.BeginTransactionAsync(Cancellation);
        var (result, messages) = await DeleteUserHandler.HandleAsync(
            command, scope.ServiceProvider.GetRequiredService<IUserRepository>(), scope.ServiceProvider.GetRequiredService<TimeProvider>(), Cancellation);
        await transaction.CommitAsync(Cancellation);
        return (
            result,
            messages.OfType<ControlPlane.Contracts.Users.UserDeletedEvent>().SingleOrDefault(),
            messages.OfType<DeleteIdentityAccountCommand>().SingleOrDefault());
    }
}
