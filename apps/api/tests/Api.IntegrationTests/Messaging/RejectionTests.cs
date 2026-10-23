using System.Net.Http.Json;
using ControlPlane.Application.Invitations.AcceptInvitation;
using ControlPlane.Application.Members;
using ControlPlane.Application.Members.ListMembers;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.ListMyTenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Messaging;

// W7: Wolverine commits whatever a handler that returns normally has changed, a failed Result included. So each of ControlPlane's
// handlers that return a Result rejects before it changes anything: a rejected command leaves no row and sends no message.
public sealed class RejectionTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateTenant_InvalidName_ChangesNoRowAndSendsNoMessage()
    {
        var admin = await _testData.AddUserAsync();
        var tenantId = Guid.NewGuid();

        var (result, session) = await InvokeForTenantAsync<Result<TenantView>>(
            tenantId, new CreateTenantCommand(admin, " ", Slug(), Email(), null));

        result.Error.Code.Value.ShouldBe("tenant.name_invalid");
        (await _testData.CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBe(0);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateTenant_SlugTaken_ChangesNoRowAndSendsNoMessage()
    {
        var admin = await _testData.AddUserAsync();
        var existing = await _testData.AddTenantAsync();
        var tenantId = Guid.NewGuid();

        var (result, session) = await InvokeForTenantAsync<Result<TenantView>>(
            tenantId, new CreateTenantCommand(admin, "Acme Ltd", existing.Slug, Email(), null));

        result.Error.Code.Value.ShouldBe("tenant.slug_taken");
        (await _testData.CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBe(0);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptInvitation_UnknownSecret_ChangesNoRowAndSendsNoMessage()
    {
        var tenant = await _testData.AddTenantAsync();
        var user = $"user_{Guid.NewGuid():N}";

        var (result, session) = await InvokeForTenantAsync<Result<MyTenantView>>(
            tenant.Id, new AcceptInvitationCommand(user, "no-such-secret", [Email()], "Ada", "Lovelace"));

        result.Error.Code.Value.ShouldBe("invitation.not_found");
        (await _testData.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{user}'")).ShouldBe(0);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptInvitation_EmailNotVerified_ChangesNoRowAndSendsNoMessage()
    {
        var (tenantId, secret) = await InviteOwnerAsync(Email());
        var user = $"user_{Guid.NewGuid():N}";

        var (result, session) = await InvokeForTenantAsync<Result<MyTenantView>>(
            tenantId, new AcceptInvitationCommand(user, secret, [Email()], "Ada", "Lovelace"));

        result.Error.Code.Value.ShouldBe("invitation.email_mismatch");
        (await PendingInvitationsAsync(tenantId)).ShouldBe(1);
        (await _testData.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{user}'")).ShouldBe(0);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAMember_ChangesNoRowAndSendsNoMessage()
    {
        var email = Email();
        var (tenantId, secret) = await InviteOwnerAsync(email);
        var member = await _testData.AddMemberAsync(tenantId);

        var (result, session) = await InvokeForTenantAsync<Result<MyTenantView>>(
            tenantId, new AcceptInvitationCommand(member, secret, [email], "Ada", "Lovelace"));

        result.Error.Code.Value.ShouldBe("member.exists");
        (await PendingInvitationsAsync(tenantId)).ShouldBe(1);
        (await database.ScalarInTenantAsync<long>(tenantId, "SELECT count(*) FROM control_plane.memberships")).ShouldBe(1);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    // A query has nothing to reject: the endpoint has checked the list request. It changes nothing and sends nothing either.
    [Fact]
    public async Task ListMembers_PageBeyondTheEnd_ChangesNoRowAndSendsNoMessage()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id);

        var (result, session) = await InvokeForTenantAsync<Result<ListPage<MemberView>>>(
            tenant.Id, new ListMembersQuery(ListRequest.Create<ListMembersSort>(2, 50, null, null).Value, RoleId: null));

        result.Value.Items.ShouldBeEmpty();
        (await database.ScalarInTenantAsync<long>(tenant.Id, "SELECT count(*) FROM control_plane.memberships")).ShouldBe(1);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    [Fact]
    public async Task ListMyTenants_APersonWhoIsNotAUserYet_ChangesNoRowAndSendsNoMessage()
    {
        var user = $"user_{Guid.NewGuid():N}";
        var result = default(Result<ListPage<MyTenantView>>)!;

        var session = await _api.TrackMessagesAsync(async () =>
            result = await Bus().InvokeAsync<Result<ListPage<MyTenantView>>>(new ListMyTenantsQuery(user, ListRequest.Create<ListMyTenantsSort>(null, null, null, null).Value), Cancellation));

        result.Value.Items.ShouldBeEmpty();
        (await _testData.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{user}'")).ShouldBe(0);
        session.Sent.AllMessages().ShouldBeEmpty();
    }

    private static string Slug() => $"tenant-{Guid.NewGuid():N}"[..20];

    private static string Email() => $"{Guid.NewGuid():N}@example.com";

    private IMessageBus Bus() => _api.Services.GetRequiredService<IHost>().MessageBus();

    private async Task<(T Result, ITrackedSession Session)> InvokeForTenantAsync<T>(Guid tenantId, object command)
    {
        var result = default(T)!;
        var session = await _api.TrackMessagesAsync(async () =>
            result = await Bus().InvokeForTenantAsync<T>(tenantId.ToString(), command, Cancellation));

        return (result, session);
    }

    // A tenant created the way a system admin does it, with its first owner's invitation delivered; returns the invitation's secret.
    private async Task<(Guid TenantId, string Secret)> InviteOwnerAsync(string ownerEmail)
    {
        _api.Identity.AddAccount($"user_{Guid.NewGuid():N}", ownerEmail);
        var slug = Slug();
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(async () =>
            (await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail })).EnsureSuccessStatusCode());
        var code = _api.Email.CodeSentTo(ownerEmail);

        return (Guid.Parse(code[..code.IndexOf('.', StringComparison.Ordinal)]), code[(code.IndexOf('.', StringComparison.Ordinal) + 1)..]);
    }

    private Task<long> PendingInvitationsAsync(Guid tenantId) =>
        database.ScalarInTenantAsync<long>(tenantId, "SELECT count(*) FROM control_plane.invitations WHERE status = 'Pending'");
}
