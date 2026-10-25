using Api.Observability;
using Sentry;
using Sentry.Protocol;

namespace Api.UnitTests.Observability;

// Section 7, "Error tracking": no email address and no invitation token leaves the process in an event. The accept link carries the
// invitation code, <tenant id>.<secret>, in its code parameter; the identity provider's sign-up link carries its ticket, a JWT, and the
// accept link, encoded, as its redirect.
public sealed class PersonalDataFilterTests
{
    private const string Address = "ali@northwind.example";

    private const string Code = "0b8f1c2e-6d4a-4f7b-9e3a-2c5d8e1f4a6b.Xq3mZ8vT1kR7nW2pL9sD4fG6hJ0aB5cE8yU1iO3tQ7w";

    private const string Ticket = "eyJhbGciOiJSUzI1NiJ9.eyJpZCI6Imluc18yYWJjIiwic2lkIjoic2l0XzEifQ.c2lnbmF0dXJlLW9mLXRoZS10aWNrZXQ";

    public static TheoryData<string, string> Texts => new()
    {
        { $"No account for {Address}", "No account for [email]" },
        { "No account for ALI.Demir+test@Northwind.Example.", "No account for [email]." },
        { "users?email_address=ali%40northwind.example", "users?email_address=[email]" },
        { $"https://app.test/invitations/accept?code={Code}", "https://app.test/invitations/accept?code=[token]" },
        { $"https://app.test/invitations/accept?code={Uri.EscapeDataString(Code)}&next=1", "https://app.test/invitations/accept?code=[token]&next=1" },
        { $"The code {Code} is unknown", "The code [token] is unknown" },
        {
            $"https://clerk.test/v1/tickets/accept?__clerk_ticket={Ticket}&redirect_url={Uri.EscapeDataString($"https://app.test/invitations/accept?code={Code}")}",
            "https://clerk.test/v1/tickets/accept?__clerk_ticket=[token]&redirect_url=https%3A%2F%2Fapp.test%2Finvitations%2Faccept%3Fcode%3D[token]"
        },
        { $"Bearer {Ticket}", "Bearer [token]" },
        { "Tenant 0b8f1c2e-6d4a-4f7b-9e3a-2c5d8e1f4a6b was renamed", "Tenant 0b8f1c2e-6d4a-4f7b-9e3a-2c5d8e1f4a6b was renamed" },
    };

    [Theory]
    [MemberData(nameof(Texts))]
    public void Filter_EventMessage_ReplacesAddressesAndTokens(string text, string filtered)
    {
        var sentryEvent = new SentryEvent { Message = new SentryMessage { Message = text, Formatted = text } };

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.Message.Message.ShouldBe(filtered);
        sentryEvent.Message.Formatted.ShouldBe(filtered);
    }

    [Fact]
    public void Filter_EventMessageParameters_ReplacesAddressesAndTokens()
    {
        var sentryEvent = new SentryEvent { Message = new SentryMessage { Message = "No invitation for {Email} with {Code}", Params = [Address, Code, 42] } };

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.Message.Params.ShouldBe(["[email]", "[token]", 42]);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Filter_ExceptionMessage_ReplacesAddressesAndTokens(string text, string filtered)
    {
        var sentryEvent = new SentryEvent { SentryExceptions = [new SentryException { Type = "System.InvalidOperationException", Value = text }] };

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.SentryExceptions.ShouldHaveSingleItem().Value.ShouldBe(filtered);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Filter_RequestUrlAndQuery_ReplacesAddressesAndTokens(string text, string filtered)
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Url = text;
        sentryEvent.Request.QueryString = text;

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.Request.Url.ShouldBe(filtered);
        sentryEvent.Request.QueryString.ShouldBe(filtered);
    }

    // A request's headers go with its event. The SDK itself replaces the Authorization header, which carries the session token.
    [Fact]
    public void Filter_RequestHeaders_ReplacesAddressesAndTokens()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Headers["Referer"] = $"https://app.test/invitations/accept?code={Code}";
        sentryEvent.Request.Headers["X-Note"] = Address;

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.Request.Headers.ShouldBe(new Dictionary<string, string> { ["Referer"] = "https://app.test/invitations/accept?code=[token]", ["X-Note"] = "[email]" }, ignoreOrder: true);
    }

    // A log's values go with its event as extra data and tags.
    [Fact]
    public void Filter_ExtraAndTags_ReplacesAddressesAndTokens()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.SetExtra("Email", Address);
        sentryEvent.SetExtra("Attempts", 3);
        sentryEvent.SetTag("Link", $"https://app.test/invitations/accept?code={Code}");

        PersonalDataFilter.Apply(sentryEvent);

        sentryEvent.Extra["Email"].ShouldBe("[email]");
        sentryEvent.Extra["Attempts"].ShouldBe(3);
        sentryEvent.Tags["Link"].ShouldBe("https://app.test/invitations/accept?code=[token]");
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Filter_Breadcrumb_ReplacesAddressesAndTokens(string text, string filtered)
    {
        var breadcrumb = new Breadcrumb(text, "default", new Dictionary<string, string> { ["value"] = text }, "Category", BreadcrumbLevel.Info);

        var result = PersonalDataFilter.Apply(breadcrumb);

        result.Message.ShouldBe(filtered);
        result.Data.ShouldBe(new Dictionary<string, string> { ["value"] = filtered });
        (result.Type, result.Category, result.Level).ShouldBe(("default", "Category", BreadcrumbLevel.Info));
    }
}
