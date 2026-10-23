using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime;
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

    // W6, S8: of two replies the tenant purge handles at once, the second fails on the saga's version, and is tried again three times,
    // inline.
    [Fact]
    public async Task ReadRetryRules_ASagaChangedByTwoMessagesAtOnce_RetriesTheSecondThreeTimesFirst()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var rule = api.Services.GetRequiredService<IWolverineRuntime>().Options.Policies.Failures
            .Single(failure => failure.Match.Description.Contains(nameof(SagaConcurrencyException), StringComparison.Ordinal));

        rule.Count().ShouldBe(3);
        rule.First().Describe().ShouldContain("Retry");
    }
}
