using ControlPlane.Contracts.TermsVersions;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.TermsVersions;

// NotifyTermsVersionCreatedHandler's fan-out of "Our terms of use change" (docs/flows/terms-of-use.md, section 6): a retry or a second
// delivery of the event sends no second email. Who gets it, which edit sends it, and the pace are in CreateTermsVersionTests and
// UpdateTermsVersionTests. The versions belong to no tenant, so each test runs on a database of its own (OwnTerms).
public sealed class TermsVersionEmailTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // A second delivery of the same event, as at-least-once delivery may bring, finds the delivery and asks the email service nothing (O4).
    // The email service's own key is no guard here: Resend keeps a key for 24 hours, and an email may wait longer, such as while emails.send
    // is off. So the test counts the requests themselves, which holds after any time.
    [Fact]
    public async Task DeliverTermsVersionCreated_ASecondTime_SendsNoSecondEmail()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        var session = await own.Api.TrackMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));
        var created = session.Sent.MessagesOf<TermsVersionCreatedEvent>().First();

        await own.Api.WaitingForMessagesAsync(async () =>
        {
            using var scope = own.Api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(created);
            return created.EventId;
        });

        own.Api.Email.Attempts.Count.ShouldBe(1);
    }

    // A try that the email service refuses is tried again with the same key, so the service sends one email.
    [Fact]
    public async Task SendTermsEmail_AFailedTry_SendsOneEmailUnderOneKey()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        own.Api.Email.FailNext(1);

        await own.Api.TrackMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        own.Api.Email.Attempts.Count.ShouldBe(2);
        own.Api.Email.Attempts.Select(attempt => attempt.Email.IdempotencyKey).Distinct().Count().ShouldBe(1);
        own.Api.Email.Sent.Count.ShouldBe(1);
    }
}
