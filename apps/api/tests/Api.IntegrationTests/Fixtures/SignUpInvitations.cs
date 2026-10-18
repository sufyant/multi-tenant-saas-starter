using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.ExpireSignUp;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

// The identity provider's sign-up of a member's invitation, driven through one test's host (section 6, "Invitation sign-up"): a tenant
// with an Owner, an invitation to a person without an account with its email sent, the id of the Clerk invitation that ours records,
// and the expiry of the invitation's lifetime. The use case classes that check a sign-up share it.
internal sealed class SignUpInvitations(ApiFactory api, Catalog catalog, Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async Task<(Guid TenantId, HttpClient Owner)> TenantWithOwnerAsync()
    {
        var tenant = await catalog.AddTenantAsync();
        return (tenant.Id, api.CreateClient(await catalog.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // An invitation to a person without an account, with its email sent: its id and the id of Clerk's invitation.
    public async Task<(Guid InvitationId, string SignUp)> InviteAsync(Guid tenantId, HttpClient owner, string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@example.com";
        var invited = await api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));
        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        return (invitationId, (await SignUpIdOfAsync(invitationId))!);
    }

    public Task<bool> ExpireAsync(Guid tenantId, Guid invitationId) =>
        api.WaitingForMessagesAsync(async () =>
        {
            using var scope = api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new ExpireSignUpCommand(tenantId, invitationId), Cancellation);
            return true;
        });

    public Task<string?> SignUpIdOfAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT sign_up_invitation_id FROM control_plane.invitations WHERE id = '{invitationId}'");
}
