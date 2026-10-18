using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain;
using ControlPlane.Domain.Tenants;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace ControlPlane.IntegrationTests.Persistence.Tenants;

public sealed class TenantDirectoryTests(Database database)
{
    [Fact]
    public async Task ResolveTenant_MemberOfAnActiveTenant_FindsTheMembership()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, user);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Value.TenantId.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task ResolveTenant_UserNotAMember_IsRefusedAsATenantThatDoesNotExist()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var other = await TestData.AddTenantAsync(database.Services);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, other, user);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    [Fact]
    public async Task ResolveTenant_UnknownTenant_IsRefusedAsATenantThatDoesNotExist()
    {
        var user = await TestData.AddUserAsync(database.Services);

        var membership = await FindMembershipAsync(Guid.CreateVersion7(), user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    [Fact]
    public async Task ResolveTenant_UnknownUser_IsRefusedAsATenantThatDoesNotExist()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);

        var membership = await FindMembershipAsync(tenant.Id, Unique.ExternalId());

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    [Fact]
    public async Task ResolveTenant_MemberOfAProvisioningTenant_IsRefusedAsATenantThatDoesNotExist()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Provisioning);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, user);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    [Fact]
    public async Task ResolveTenant_MemberOfASuspendedTenant_IsRefusedBecauseTheTenantIsSuspended()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Suspended);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, user);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantSuspended);
    }

    // T3: a stranger cannot tell a suspended tenant from one that does not exist: both answer the handlers' tenant.not_found.
    [Fact]
    public async Task ResolveTenant_StrangerToASuspendedTenant_IsRefusedAsATenantThatDoesNotExist()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Suspended);
        var user = await TestData.AddUserAsync(database.Services);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    [Fact]
    public async Task ResolveTenant_MemberOfAClosedTenant_IsRefusedBecauseTheTenantIsClosed()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Closed);
        var user = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, user);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantClosed);
    }

    // T3: a stranger cannot tell a closed tenant from one that does not exist.
    [Fact]
    public async Task ResolveTenant_StrangerToAClosedTenant_IsRefusedAsATenantThatDoesNotExist()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Closed);
        var user = await TestData.AddUserAsync(database.Services);

        var membership = await FindMembershipAsync(tenant.Id, user.ExternalId);

        membership.Error.Code.ShouldBe(ControlPlaneErrorCodes.TenantNotFound);
    }

    private async Task<Result<TenantMembership>> FindMembershipAsync(Guid tenantId, string externalUserId)
    {
        await using var scope = database.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ITenantDirectory>()
            .FindMembershipAsync(tenantId, externalUserId, TestContext.Current.CancellationToken);
    }
}
