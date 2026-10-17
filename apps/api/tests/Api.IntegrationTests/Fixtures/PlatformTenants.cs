using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// Tenants that the platform makes through the API, as a system admin does: the tenant creation saga invites the first Owner, and a person
// with an account accepts an invitation with the code of its email. A test of the platform's screens builds each state of an Owner this
// way, so the summary the tenant keeps (R12) comes from the commands, not from rows a test writes.
internal sealed class PlatformTenants(Database database, ApiFactory api)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // A new active tenant, after every step of its creation, whose first Owner is invited at the address.
    public async Task<Guid> CreateAsync(HttpClient admin, string ownerEmail, string name = "Acme Ltd")
    {
        var created = await api.WaitingForMessagesAsync(() =>
            admin.CreateTenantAsync(new { name, slug = $"tenant-{Guid.NewGuid():N}"[..20], ownerEmail }));
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    // The id of the invitation a request answered.
    public static async Task<Guid> InvitationIdOfAsync(HttpResponseMessage invited) =>
        (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    // The tenant's latest invitation: for a tenant that has one invitation, or whose invitations a fake clock does not create at one
    // moment.
    public Task<Guid> LatestInvitationAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>(
            $"SELECT id FROM control_plane.invitations WHERE tenant_id = '{tenantId}' ORDER BY created_at DESC, id DESC LIMIT 1");

    // A new person with an account at the address accepts the invitation with the code of its email. Returns their identity provider id.
    public async Task<(string UserId, HttpResponseMessage Accepted)> AcceptAsync(Guid invitationId, string email, string firstName = "Ada", string lastName = "Lovelace")
    {
        var userId = $"user_{Guid.NewGuid():N}";
        api.Identity.AddAccount(userId, email);
        var accepted = await api.WaitingForMessagesAsync(() => AcceptAsync(userId, api.Email.CodeSentFor(invitationId), firstName, lastName));
        return (userId, accepted);
    }

    public Task<HttpResponseMessage> AcceptAsync(string userId, string code, string firstName = "Ada", string lastName = "Lovelace") =>
        api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName, lastName }, Cancellation);

    public static string Email() => $"{Guid.NewGuid():N}@example.com";
}
