using ControlPlane.Contracts.TermsVersions;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Notifications.DeliverNotification;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.TermsVersions;

// NotifyTermsVersionCreatedHandler's fan-out of "Our terms of use change" (docs/flows/terms-of-use.md, section 6): who gets it, which edit
// sends it, that a retry or a second delivery of the event sends no second email, and the pace. The versions belong to no tenant, so each
// test runs on a database of its own (OwnTerms).
public sealed class TermsVersionEmailTests(Database database)
{
    private const string AcceptAtSignIn = "You accept them the next time you sign in.";

    private const string NothingToDo = "You do not need to do anything.";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Every active tenant user, once even when they are a member of two tenants. No suspended user, no deleted user, no user without a
    // membership, and not the system admin who created the version (U3).
    [Fact]
    public async Task CreateTermsVersion_UsersOfTwoTenants_EmailsEveryActiveTenantUserOnce()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (first, _) = await own.Data.AddTenantAsync();
        var (second, _) = await own.Data.AddTenantAsync();
        var both = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var onlyFirst = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var onlySecond = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var (inBoth, _) = await own.AddTenantUserAsync(first, both);
        await own.Data.AddMemberAsync(second, inBoth);
        await own.AddTenantUserAsync(first, onlyFirst);
        await own.AddTenantUserAsync(second, onlySecond);
        var (suspended, _) = await own.AddTenantUserAsync(first);
        await own.Data.SuspendUserAsync(suspended);
        var (deleted, _) = await own.AddTenantUserAsync(second);
        await own.Data.DeleteUserAsync(deleted);
        await own.Data.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        await own.Data.AddMemberAsync(first, own.AdminId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        own.Api.Email.Sent.Select(email => email.To).ShouldBe([both, onlyFirst, onlySecond], ignoreOrder: true);
    }

    // "Accept at your next sign-in" only when the version requires acceptance; else the person does nothing.
    [Fact]
    public async Task CreateTermsVersion_RequiringNoAcceptance_SaysThereIsNothingToDo()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), requiresAcceptance: false));

        var text = own.Api.Email.Sent.Single().Text;
        text.ShouldContain(NothingToDo);
        text.ShouldNotContain(AcceptAtSignIn);
    }

    // The edit that turns the email on sends one email, with the values after the edit.
    [Fact]
    public async Task UpdateTermsVersion_TheEmailTurnedOn_EmailsTheValuesAfterTheEdit()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Api.WaitingForMessagesAsync(() =>
            own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), summary: "Before the edit.", requiresAcceptance: false, sendsEmail: false));

        await own.Api.WaitingForMessagesAsync(() => own.Admin.UpdateTermsVersionAsync(
            versionId, new DateOnly(2027, 3, 15), "https://brand.example/terms/2027", "After the edit.", requiresAcceptance: true));

        var email = own.Api.Email.Sent.ShouldHaveSingleItem();
        email.Text.ShouldContain("15 Mar 2027");
        email.Text.ShouldContain("After the edit.");
        email.Text.ShouldContain("https://brand.example/terms/2027");
        email.Text.ShouldContain(AcceptAtSignIn);
        email.Text.ShouldNotContain("Before the edit.");
    }

    // An edit that leaves the email off sends nothing, and so does an edit after the email went.
    [Fact]
    public async Task UpdateTermsVersion_ChangingNothingOfTheEmail_EmailsNobody()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        var silent = await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), sendsEmail: false));

        await own.Api.WaitingForMessagesAsync(() => own.Admin.UpdateTermsVersionAsync(silent, TermsRequests.Today.AddDays(12), summary: "Still silent.", sendsEmail: false));
        var stillSilent = own.Api.Email.Sent.Count;
        (await own.Admin.CancelTermsVersionAsync(silent)).EnsureSuccessStatusCode();
        var told = await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));
        await own.Api.WaitingForMessagesAsync(() => own.Admin.UpdateTermsVersionAsync(told, TermsRequests.Today.AddDays(12), summary: "A later summary."));

        (stillSilent, own.Api.Email.Sent.Count).ShouldBe((0, 1));
    }

    // A second delivery of the same event, as at-least-once delivery may bring, asks the email service again under the same key: the email
    // only type writes no bell's notification to find, and the key, the notification's id, keeps the copy to one email (O4; Resend keeps a
    // key for 24 hours).
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

        own.Api.Email.Attempts.Select(attempt => attempt.Email.IdempotencyKey).Distinct().Count().ShouldBe(1);
        own.Api.Email.Sent.Count.ShouldBe(1);
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

    // Three users at one delivery a second: the first goes at once, the next a second later, the last two seconds later.
    [Fact]
    public async Task CreateTermsVersion_ThreeUsersAtOneDeliveryASecond_SchedulesOneDeliveryEachSecond()
    {
        await using var own = await OwnTerms.CreateAsync(database, new Dictionary<string, string?> { ["Notifications:Pace:DeliveriesPerSecond"] = "1" });
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        await own.AddTenantUserAsync(tenantId);
        await own.AddTenantUserAsync(tenantId);

        var session = await own.Api.TrackMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        var later = session.Scheduled.Envelopes().Where(envelope => envelope.Message is DeliverNotificationCommand).Select(envelope => envelope.ScheduledTime!.Value).Order().ToList();
        var atOnce = session.Sent.MessagesOf<DeliverNotificationCommand>().Count() - later.Count;
        (atOnce, later.Count).ShouldBe((1, 2));
        Math.Round((later[1] - later[0]).TotalSeconds).ShouldBe(1);
    }
}
