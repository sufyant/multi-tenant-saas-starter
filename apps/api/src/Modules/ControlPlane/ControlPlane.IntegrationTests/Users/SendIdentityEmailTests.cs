using ControlPlane.Application.Users.ReplaceIdentityEmail;
using ControlPlane.Application.Users.SendIdentityEmail;

namespace ControlPlane.IntegrationTests.Users;

// A7: the step calls the identity provider with no transaction (section 3), then asks the step before to read our record again.
public sealed class SendIdentityEmailTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendIdentityEmail_AnAddress_ReplacesItAtTheProviderAndAsksToReadOurRecordAgain()
    {
        var userId = Guid.CreateVersion7();
        var identity = new FakeIdentityProvider();

        var again = await SendIdentityEmailHandler.HandleAsync(new SendIdentityEmailCommand(userId, "user_ada", "ada@now.example"), identity, Cancellation);

        identity.Replaced.ShouldBe([("user_ada", "ada@now.example")]);
        again.ShouldBe(new ReplaceIdentityEmailCommand(userId, "ada@now.example"));
    }
}
