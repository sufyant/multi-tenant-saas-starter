using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.ExpireSignUp;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Invitations;

// ExpireSignUpCommand: when an invitation's lifetime is over, ControlPlane withdraws its Clerk invitation, so nobody signs up with its
// link (section 6, "Invitation sign-up"). Clerk is a fake at our port; the adapter's own requests are pinned down in
// ClerkIdentityProviderTests.
public sealed class ExpireSignUpTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The withdrawal at the end of the invitation's life is scheduled when the link goes out; here it arrives, once the lifetime is over.
    [Fact]
    public async Task ExpireSignUp_LifetimeOver_WithdrawsTheClerkInvitationOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);
        _time.Advance(TimeSpan.FromDays(7));

        await ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    // The messaging tool schedules by its own clock; a message that arrives before the module's clock ends the lifetime withdraws nothing.
    [Fact]
    public async Task ExpireSignUp_BeforeTheLifetimeIsOver_WithdrawsNothing()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);

        await ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.ShouldNotContain(signUp);
    }

    private async Task<(Guid TenantId, HttpClient Owner)> TenantWithOwnerAsync()
    {
        var tenant = await _catalog.AddTenantAsync();
        return (tenant.Id, _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // An invitation to a person without an account, with its email sent: its id and the id of Clerk's invitation.
    private async Task<(Guid InvitationId, string SignUp)> InviteAsync(Guid tenantId, HttpClient owner, string? email = null)
    {
        email ??= NewEmail();
        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));
        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        return (invitationId, (await SignUpInvitationIdAsync(invitationId))!);
    }

    private Task<bool> ExpireAsync(Guid tenantId, Guid invitationId) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new ExpireSignUpCommand(tenantId, invitationId), Cancellation);
            return true;
        });

    private Task<string?> SignUpInvitationIdAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT sign_up_invitation_id FROM control_plane.invitations WHERE id = '{invitationId}'");

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";
}
