using Notifications.Application.Email;
using SharedKernel;

namespace Notifications.UnitTests.Email;

// The one renderer of every email: it fills the placeholders of a generated template, encodes each value for HTML in the HTML part and
// leaves the text part and the subject as text. A placeholder without a value, a value without a placeholder and a link that is not an
// absolute https URL (or mailto: for a contact) each stop the email, so a mistake fails here, not in a customer's inbox.
public sealed class EmailTemplateTests
{
    private static readonly EmailTemplate Greeting = new(
        Subject: "Hello {{name}}",
        Html: "<p>Hello {{name}}</p><a href=\"{{link}}\">{{link}}</a>",
        Text: "Hello {{name}}\n{{link}}");

    private static readonly EmailValue Link = EmailValue.Link("link", new Uri("https://web.example/invitations/accept?code=a&b"));

    private static readonly string[] Emails = ["invitation", "deletion-warning"];

    public static TheoryData<string, Language> EveryGeneratedEmail =>
        [.. from email in Emails from language in Enum.GetValues<Language>() select (email, language)];

    // OWASP, "Cross Site Scripting Prevention Cheat Sheet", rule 1: HTML-encode a value put into an element or a quoted attribute.
    [Fact]
    public void WriteEmail_AValueWithHtml_EscapesItInTheHtmlAndKeepsItAsWrittenInTheTextAndTheSubject()
    {
        var email = Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "<b>Ayşe</b> & co"), Link]);

        email.Html.ShouldBe(
            "<p>Hello &lt;b&gt;Ayşe&lt;/b&gt; &amp; co</p>"
            + "<a href=\"https://web.example/invitations/accept?code=a&amp;b\">https://web.example/invitations/accept?code=a&amp;b</a>");
        email.Text.ShouldBe("Hello <b>Ayşe</b> & co\nhttps://web.example/invitations/accept?code=a&b");
        email.Subject.ShouldBe("Hello <b>Ayşe</b> & co");
    }

    [Fact]
    public void WriteEmail_AQuoteInAValue_EscapesItInTheHtml()
    {
        var email = Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "\"O'Hara\""), Link]);

        email.Html.ShouldStartWith("<p>Hello &quot;O&#39;Hara&quot;</p>");
    }

    [Fact]
    public void WriteEmail_AnAddressAndAKey_GoOnTheMessage()
    {
        var email = Greeting.Write("ada@example.test", "invite/1", [EmailValue.Text("name", "Ada"), Link]);

        (email.To, email.IdempotencyKey).ShouldBe(("ada@example.test", "invite/1"));
    }

    [Fact]
    public void WriteEmail_APlaceholderWithoutAValue_IsRefused()
    {
        var write = () => Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "Ada")]);

        write.ShouldThrow<InvalidOperationException>().Message.ShouldContain("{{link}}");
    }

    [Fact]
    public void WriteEmail_AValueWithoutAPlaceholder_IsRefused()
    {
        var write = () => Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "Ada"), Link, EmailValue.Text("roleName", "Admin")]);

        write.ShouldThrow<InvalidOperationException>().Message.ShouldContain("roleName");
    }

    [Theory]
    [InlineData("http://web.example/invitations/accept?code=x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:support@starter.example")]
    [InlineData("/invitations/accept?code=x")]
    public void WriteEmail_ALinkThatIsNotAnAbsoluteHttpsUrl_IsRefused(string link)
    {
        var write = () => Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "Ada"), EmailValue.Link("link", new Uri(link, UriKind.RelativeOrAbsolute))]);

        write.ShouldThrow<InvalidOperationException>().Message.ShouldContain("link");
    }

    [Theory]
    [InlineData("mailto:support@starter.example")]
    [InlineData("https://help.starter.example/contact")]
    public void WriteEmail_AContactByMailtoOrHttps_IsAccepted(string contact)
    {
        var email = Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "Ada"), EmailValue.Contact("link", new Uri(contact))]);

        email.Text.ShouldEndWith(contact);
    }

    [Theory]
    [InlineData("http://help.starter.example/contact")]
    [InlineData("ftp://help.starter.example/contact")]
    public void WriteEmail_AContactThatIsNeitherMailtoNorHttps_IsRefused(string contact)
    {
        var write = () => Greeting.Write("ada@example.test", "key", [EmailValue.Text("name", "Ada"), EmailValue.Contact("link", new Uri(contact))]);

        write.ShouldThrow<InvalidOperationException>();
    }

    // Each generated email in each language of the contract exists, as a subject, an HTML part and a text part, and holds only
    // placeholders of the form {{name}}.
    [Theory]
    [MemberData(nameof(EveryGeneratedEmail))]
    public void ReadEmail_EveryGeneratedEmailInEveryLanguage_HasItsThreeParts(string email, Language language)
    {
        var template = EmailTemplate.Of(email, language);

        template.Subject.ShouldNotBeNullOrWhiteSpace();
        template.Html.ShouldStartWith("<!DOCTYPE html");
        template.Text.ShouldStartWith("Starter\n\n");
    }

    // The brand of packages/brand, which the render script copies beside the emails.
    [Fact]
    public void ReadBrand_TheGeneratedBrand_IsTheProductsNameAndSupportContact()
    {
        (EmailTemplate.Brand.Name, EmailTemplate.Brand.SupportUrl).ShouldBe(("Starter", new Uri("mailto:support@starter.example")));
    }
}
