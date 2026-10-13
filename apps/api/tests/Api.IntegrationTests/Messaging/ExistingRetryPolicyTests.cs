using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Api.IntegrationTests.Messaging;

public sealed class ExistingRetryPolicyTests(Database database)
{
    [Theory]
    [InlineData("ActivateTenant", "Retry inline with no delay")]
    [InlineData("CancelTenant", "Retry inline with no delay")]
    [InlineData("CancelInvitation", "Retry inline with no delay")]
    [InlineData("OwnerInvitationReady", "Retry inline with a delay of 00:00:00.01")]
    public async Task ReadRetryRules_TenantStepsAndInvitation_KeepTheirMessageSpecificRules(string message, string firstAction)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var chain = api.Services.GetRequiredService<HandlerGraph>().Chains.Single(chain => chain.MessageType.Name == message);
        _ = api.Services.GetRequiredService<HandlerGraph>().HandlerFor(chain.MessageType);

        var rule = chain.Failures.First();

        rule.Count().ShouldBe(3);
        rule.First().Describe().ShouldContain(firstAction);
    }

    [Fact]
    public async Task SelectRetryRule_SagaConcurrencyException_KeepsTheOriginalImmediateRetries()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var failures = api.Services.GetRequiredService<HandlerGraph>().Failures;
        var exception = new SagaConcurrencyException("The saga changed.");
        var envelope = new Envelope(new RetryProbe(Guid.NewGuid())) { Destination = new Uri("local://retry/"), Attempts = 1 };

        var rule = failures.First(rule => rule.Match.Matches(exception));
        rule.TryCreateContinuation(exception, envelope, out var continuation).ShouldBeTrue();

        rule.Count().ShouldBe(3);
        continuation.GetType().Name.ShouldBe("RetryInlineContinuation");
        rule.First().Describe().ShouldBe("Retry inline with no delay");
    }
}
