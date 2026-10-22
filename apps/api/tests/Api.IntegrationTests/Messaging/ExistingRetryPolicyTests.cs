using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Api.IntegrationTests.Messaging;

public sealed class ExistingRetryPolicyTests(Database database)
{
    [Theory]
    [InlineData("InvitationReadyEvent", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("OpenSignUpCommand", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("WithdrawSignUpCommand", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("DeleteIdentityAccountCommand", "Retry inline with a delay of 00:00:00.01")]
    public async Task ReadRetryRules_InvitationEmailsAndIdentityProviderCalls_KeepTheirMessageSpecificRules(string message, string firstAction)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var chain = api.Services.GetRequiredService<HandlerGraph>().Chains.Single(chain => chain.MessageType.Name == message);
        _ = api.Services.GetRequiredService<HandlerGraph>().HandlerFor(chain.MessageType);

        var rule = chain.Failures.First();

        rule.Count().ShouldBe(3);
        rule.First().Describe().ShouldContain(firstAction);
    }
}
