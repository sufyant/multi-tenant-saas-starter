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

namespace ControlPlane.IntegrationTests.Tenants;

// ActivateTenantCommand, run the way Wolverine runs a step of the tenant creation saga: it writes the catalog in the new tenant's
// transaction, and it may run twice (S7).
public sealed class ActivateTenantTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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
