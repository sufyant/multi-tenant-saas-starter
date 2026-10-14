using System.Security.Cryptography;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Svix;

namespace ControlPlane.IntegrationTests.Clerk;

// Clerk's webhook is a system we do not own; these tests pin down how the adapter reads its signed events. The signing secret is
// random, made when the tests run.
public sealed class ClerkIdentityProviderWebhookTests(Database database)
{
    private static readonly string SigningSecret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void ReadClerkEvent_UserDeleted_ReadsAsDeleteUser()
    {
        var read = Read("""{"type":"user.deleted","object":"event","data":{"id":"user_1","object":"user","deleted":true}}""");

        read.Value.ShouldBe(new DeleteUser("user_1"));
    }

    // The module's real adapter, as the host registers it, with a delivery signed now with the endpoint's secret.
    private Result<object?> Read(string payload)
    {
        var webhook = database.BuildServices(settings: new() { ["ControlPlane:Clerk:WebhookSigningSecret"] = SigningSecret })
            .GetRequiredService<IIdentityProviderWebhook>();
        var now = DateTimeOffset.UtcNow;
        Dictionary<string, string> headers = new()
        {
            ["svix-id"] = "msg_1",
            ["svix-timestamp"] = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["svix-signature"] = new Webhook(SigningSecret).Sign("msg_1", now, payload),
        };

        return webhook.Read(payload, name => headers.GetValueOrDefault(name));
    }
}
