using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.ExpireSignUp;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Invitations;

// ExpireSignUpCommand: when an invitation's lifetime is over, ControlPlane withdraws its Clerk invitation, so nobody signs up with its
// link (section 6, "Invitation sign-up"). Clerk is a fake at our port; the adapter's own requests are pinned down in
// ClerkIdentityProviderTests.
public sealed class ExpireSignUpTests(Database database) : IAsyncLifetime
{
    private readonly Catalog _catalog = new(database);
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    private SignUpInvitations SignUps => new(_api, _catalog, database);

    // The withdrawal at the end of the invitation's life is scheduled when the link goes out; here it arrives, once the lifetime is over.
    [Fact]
    public async Task ExpireSignUp_LifetimeOver_WithdrawsTheClerkInvitationOnce()
    {
        var (tenantId, owner) = await SignUps.TenantWithOwnerAsync();
        var (invitationId, signUp) = await SignUps.InviteAsync(tenantId, owner);
        _time.Advance(TimeSpan.FromDays(7));

        await SignUps.ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    // The messaging tool schedules by its own clock; a message that arrives before the module's clock ends the lifetime withdraws nothing.
    [Fact]
    public async Task ExpireSignUp_BeforeTheLifetimeIsOver_WithdrawsNothing()
    {
        var (tenantId, owner) = await SignUps.TenantWithOwnerAsync();
        var (invitationId, signUp) = await SignUps.InviteAsync(tenantId, owner);

        await SignUps.ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.ShouldNotContain(signUp);
    }
}
