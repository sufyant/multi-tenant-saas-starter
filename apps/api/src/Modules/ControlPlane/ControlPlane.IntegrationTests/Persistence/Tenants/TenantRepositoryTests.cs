using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Persistence.Tenants;

// Tenant-owned tenants rows are reached through the tenant repository, and row level security keeps each tenant to its own.
public sealed class TenantRepositoryTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadMemberships_InATenant_ReturnsOnlyItsOwn()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var other = await TestData.AddTenantAsync(database.Services);
        var member = await TestData.AddUserAsync(database.Services);
        var outsider = await TestData.AddUserAsync(database.Services);
        await TestData.AddMemberAsync(database.Services, tenant, member);
        await TestData.AddMemberAsync(database.Services, other, outsider);

        var members = await InTenant.ReadAsync(database.Services, tenant.Id, scope =>
            ((TenantRepository)scope.GetRequiredService<ITenantRepository>()).Memberships.Select(membership => membership.UserId).ToListAsync(Cancellation));

        members.ShouldBe([member.Id]);
    }

    // One repository method writes the membership, the tenant's summary and the user's count (R13).
    [Fact]
    public async Task AddMembership_InATenant_BelongsToThatTenantAndIsCountedAndSummarized()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var user = await TestData.AddUserAsync(database.Services);

        await InTenant.RunAsync(database.Services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            var locked = (await tenants.FindTenantForUpdateAsync(Cancellation))!;
            await tenants.AddMembershipAsync(locked, user.Id, BuiltInRoles.Member.Id, DateTimeOffset.UtcNow, Cancellation);
            return SharedKernel.Result.Success();
        });

        var memberships = await InTenant.ReadAsync(database.Services, tenant.Id, scope =>
            ((TenantRepository)scope.GetRequiredService<ITenantRepository>()).Memberships.Select(membership => membership.TenantId).ToListAsync(Cancellation));
        memberships.ShouldBe([tenant.Id]);
        (await TestData.MembershipCountOfAsync(database.Services, user)).ShouldBe(1);
        (await TestData.MemberCountOfAsync(database.Services, tenant)).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveMembership_InATenant_IsGoneAndUncountedAndSummarized()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var user = await TestData.AddUserAsync(database.Services);
        await InTenant.RunAsync(database.Services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            await tenants.AddMembershipAsync((await tenants.FindTenantForUpdateAsync(Cancellation))!, user.Id, BuiltInRoles.Member.Id, DateTimeOffset.UtcNow, Cancellation);
            return SharedKernel.Result.Success();
        });

        await InTenant.RunAsync(database.Services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            var locked = (await tenants.FindTenantForUpdateAsync(Cancellation))!;
            await tenants.RemoveMembershipAsync(locked, (await tenants.FindMembershipAsync(user.Id, Cancellation))!, Cancellation);
            return SharedKernel.Result.Success();
        });

        (await TestData.MembershipCountOfAsync(database.Services, user)).ShouldBe(0);
        (await TestData.MemberCountOfAsync(database.Services, tenant)).ShouldBe(0);
    }

    // K6k: a command that changes an invitation or a membership and saves it without the summary does not commit, so the summary
    // never says less than the rows (R12).
    [Fact]
    public async Task CommitTransaction_AnInvitationSavedWithoutTheSummary_IsRefusedAndAddsNothing()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var inviter = await TestData.AddUserAsync(database.Services);

        var commit = () => InTenant.RunAsync(database.Services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            await tenants.FindTenantForUpdateAsync(Cancellation);
            tenants.Add(OwnerInvitation(tenant, inviter));
            await tenants.SaveChangesAsync(Cancellation);
        });

        await commit.ShouldThrowAsync<InvalidOperationException>();
        (await InvitationsOfAsync(tenant)).ShouldBe(0);
    }

    [Fact]
    public async Task CommitTransaction_AnInvitationSavedWithTheSummary_CommitsAndSummarizes()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        var inviter = await TestData.AddUserAsync(database.Services);

        await InTenant.RunAsync(database.Services, tenant.Id, async scope =>
        {
            var tenants = scope.GetRequiredService<ITenantRepository>();
            var locked = (await tenants.FindTenantForUpdateAsync(Cancellation))!;
            tenants.Add(OwnerInvitation(tenant, inviter));
            await tenants.SaveAsync(locked, Cancellation);
        });

        (await InvitationsOfAsync(tenant)).ShouldBe(1);
        (await TestData.OwnerInvitationEmailOfAsync(database.Services, tenant)).ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task ReadMemberships_OutsideATenant_IsRefused()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var tenants = (TenantRepository)scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var read = () => tenants.Memberships.ToListAsync(Cancellation);

        await read.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FindInvitationByHash_OfAnotherTenant_FindsNothing()
    {
        var email = Unique.Email();
        var (_, _, ready) = await Handlers.CreateActiveTenantAsync(database.Services, email);
        var tokenHash = InvitationSecret.Hash(Handlers.SecretOf(Handlers.CodeOf(ready.AcceptLink)));
        var tenant = await TestData.AddTenantAsync(database.Services);

        var found = await InTenant.ReadAsync(database.Services, tenant.Id, scope =>
            scope.GetRequiredService<ITenantRepository>().FindInvitationForUpdateAsync(tokenHash, Cancellation));

        found.ShouldBeNull();
    }

    [Fact]
    public async Task FindInvitationById_OfAnotherTenant_FindsNothing()
    {
        var (_, _, ready) = await Handlers.CreateActiveTenantAsync(database.Services, Unique.Email());
        var invitationId = ready.InvitationId;
        var tenant = await TestData.AddTenantAsync(database.Services);

        var found = await InTenant.ReadAsync(database.Services, tenant.Id, scope =>
            scope.GetRequiredService<ITenantRepository>().FindInvitationForUpdateAsync(invitationId, Cancellation));

        found.ShouldBeNull();
    }

    // Tenant creation adds the tenant it runs in, and only that one.
    [Fact]
    public async Task AddTenant_OtherThanTheDeclaredTenant_IsRefused()
    {
        var other = Domain.Tenants.Tenant.Create(Guid.CreateVersion7(), "Other Ltd", Unique.Slug(), DateTimeOffset.UtcNow).Value;

        var add = () => InTenant.ReadAsync(database.Services, Guid.CreateVersion7(), scope =>
            scope.GetRequiredService<ITenantRepository>().TryAddAsync(other, Cancellation));

        await add.ShouldThrowAsync<InvalidOperationException>();
    }

    private static Invitation OwnerInvitation(Tenant tenant, User inviter) =>
        Invitation.Create(
            Guid.CreateVersion7(),
            tenant.Id,
            "ada@example.com",
            BuiltInRoles.Owner,
            inviter.Id,
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7),
            InvitationSecret.Generate(),
            SharedKernel.Language.En);

    private Task<int> InvitationsOfAsync(Tenant tenant) =>
        InTenant.ReadAsync(database.Services, tenant.Id, scope =>
            scope.GetRequiredService<ITenantRepository>().CountInvitationsSinceAsync(DateTimeOffset.MinValue, Cancellation));
}
