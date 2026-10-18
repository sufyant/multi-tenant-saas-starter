using System.Text;
using ControlPlane.Application.Invitations.CancelInvitation;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.CancelTenant;
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

// CancelTenantCommand, the saga's compensation of a tenant that is still provisioning, run the way Wolverine runs a step of the tenant
// creation saga: it writes ControlPlane's tables in the new tenant's transaction, and it may run twice (S7).
public sealed class CancelTenantTests(Database database)
{
    [Fact]
    public async Task CancelTenant_Provisioning_CancelsTheTenantWithItsReasonAndTheInvitation()
    {
        var (tenantId, invitationId) = await Handlers.StartTenantCreationAsync(database.Services);

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        cancelled.ShouldBe(new CancelTenantReply(tenantId));
        (await StatusOfAsync(tenantId)).ShouldBe("Cancelled");
        (await ScalarAsync<string>($"SELECT cancellation_reason FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBe("activation_failed");
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Cancelled");
    }

    [Fact]
    public async Task CancelTenant_Repeated_KeepsTheFirstReason()
    {
        var (tenantId, invitationId) = await Handlers.StartTenantCreationAsync(database.Services);
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
        var (tenantId, invitationId) = await Handlers.StartTenantCreationAsync(database.Services);
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
        var (tenantId, invitationId) = await Handlers.StartTenantCreationAsync(database.Services);
        await Handlers.ActivateAsync(database.Services, tenantId, new ActivateTenantCommand(tenantId, invitationId, AcceptLink()));
        await ScalarAsync<object>($"UPDATE control_plane.tenants SET status = 'Suspended' WHERE id = '{tenantId}'");

        var cancelled = await Handlers.CancelTenantAsync(database.Services, tenantId, new CancelTenantCommand(tenantId, invitationId, "activation_failed"));

        cancelled.ShouldBeNull();
        (await StatusOfAsync(tenantId)).ShouldBe("Suspended");
        (await ScalarAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'")).ShouldBe("Pending");
    }

    private static Uri AcceptLink() => new($"{Database.AcceptUrl}?code={Guid.CreateVersion7()}.secret");

    private Task<string> StatusOfAsync(Guid tenantId) => ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<T> ScalarAsync<T>(string sql) => database.ScalarAsSuperuserAsync<T>(sql);
}
