using Notifications.Application.Email;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace Api.IntegrationTests.Messaging;

public sealed class ExistingRetryPolicyTests(Database database)
{
    [Theory]
    [InlineData("InvitationReadyEvent", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("TenantPurgeWarningReadyEvent", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("OpenSignUpCommand", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("WithdrawSignUpCommand", "Retry inline with a delay of 00:00:00.01")]
    [InlineData("DeleteIdentityAccountCommand", "Retry inline with a delay of 00:00:00.01")]
    public async Task ReadRetryRules_InvitationEmailsAndIdentityProviderCalls_KeepTheirMessageSpecificRules(string message, string firstAction)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var chain = api.Services.GetRequiredService<HandlerGraph>().Chains.Single(chain => chain.MessageType.Name == message);
        _ = api.Services.GetRequiredService<HandlerGraph>().HandlerFor(chain.MessageType);

        var rule = chain.Failures.First(failure => !failure.Match.Description.Contains(nameof(EmailsPausedException), StringComparison.Ordinal));

        rule.Count().ShouldBe(3);
        rule.First().Describe().ShouldContain(firstAction);
    }

    // B5, B7, B8: each call to Stripe from a queued message that fails is tried again after each configured pause (Billing:Provider).
    [Theory]
    [InlineData("SyncSubscriptionCommand")]
    [InlineData("ExpireCheckoutPagesCommand")]
    [InlineData("SetCancelAtPeriodEndCommand")]
    [InlineData("CancelSubscriptionCommand")]
    public async Task ReadRetryRules_ACallToStripeFromAQueuedMessage_KeepsItsMessageSpecificRule(string message)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var chain = api.Services.GetRequiredService<HandlerGraph>().Chains.Single(chain => chain.MessageType.Name == message);
        _ = api.Services.GetRequiredService<HandlerGraph>().HandlerFor(chain.MessageType);

        var rule = chain.Failures.First();

        rule.Count().ShouldBe(3);
        rule.First().Describe().ShouldContain("Retry inline with a delay of 00:00:00.01");
    }

    // G7: while the kill switch emails.send is off, every email waits: its first rule schedules it again, with no end.
    [Theory]
    [InlineData("InvitationReadyEvent")]
    [InlineData("TenantPurgeWarningReadyEvent")]
    [InlineData("VerificationCodeReadyEvent")]
    [InlineData("EmailChangeNoticeReadyEvent")]
    [InlineData("UserDeletionConfirmationReadyEvent")]
    [InlineData("SendNotificationEmailCommand")]
    public async Task ReadRetryRules_AnEmailWhileTheKillSwitchIsOff_SchedulesItAgainWithNoEnd(string message)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var chain = api.Services.GetRequiredService<HandlerGraph>().Chains.Single(chain => chain.MessageType.Name == message);
        _ = api.Services.GetRequiredService<HandlerGraph>().HandlerFor(chain.MessageType);

        var rule = chain.Failures.First();

        (rule.Match.Description.Contains(nameof(EmailsPausedException), StringComparison.Ordinal), rule.First().Describe()).ShouldBe((true, "Schedule Retry in 60 seconds"));
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
