using ControlPlane.Contracts.Users;
using Notifications.Application.Email;
using Notifications.Application.Users;
using Notifications.Application.Users.SendEmailChangeNoticeEmail;
using Notifications.Application.Users.SendUserDeletionConfirmationEmail;
using Notifications.Application.Users.SendVerificationCodeEmail;
using SharedKernel;

namespace Notifications.UnitTests.Users.SendVerificationCodeEmail;

// The emails about a person's own account (section 6, "Verification codes"; A7; A8): the code to a new address or for the deletion, the
// notice to the old address after a change, and the confirmation of a deletion, each in the person's language, from the generated
// templates of packages/emails, in HTML and plain text.
public sealed class SendVerificationCodeEmailHandlerTests
{
    private static readonly Guid UserId = new("0199a8f0-0000-7000-8000-000000000a01");

    private static readonly Guid CodeId = new("0199a8f0-0000-7000-8000-000000000a02");

    private readonly RecordingEmailChannel _channel = new();

    public static TheoryData<Language> Languages => [.. Enum.GetValues<Language>()];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(VerificationEmailKind.EmailChange, "Your code to change your email address")]
    [InlineData(VerificationEmailKind.AccountDeletion, "Your code to delete your account")]
    public async Task SendVerificationCodeEmail_EachKind_SendsTheDigitsToTheAddressUnderTheCodesKey(VerificationEmailKind kind, string subject)
    {
        await SendVerificationCodeEmailHandler.HandleAsync(Code(kind, Language.En), _channel, Cancellation);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject, email.IdempotencyKey).ShouldBe(("ada@new.example", subject, $"verification-code/{CodeId}"));
        email.Text.ShouldContain("\n\n381906\n\n");
        email.Html.ShouldContain("381906");
    }

    // Every generated email in every language has exactly the placeholders the handlers fill; a missing or an extra one fails here.
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task SendAccountEmails_EveryLanguage_FillEveryPlaceholder(Language language)
    {
        await SendVerificationCodeEmailHandler.HandleAsync(Code(VerificationEmailKind.EmailChange, language), _channel, Cancellation);
        await SendVerificationCodeEmailHandler.HandleAsync(Code(VerificationEmailKind.AccountDeletion, language), _channel, Cancellation);
        await SendEmailChangeNoticeEmailHandler.HandleAsync(Notice(language), _channel, Cancellation);
        await SendUserDeletionConfirmationEmailHandler.HandleAsync(new UserDeletionConfirmationReadyEvent(UserId, "ada@example.com", language), _channel, Cancellation);

        _channel.Sent.Count.ShouldBe(4);
        _channel.Sent.ShouldAllBe(email => email.Html.StartsWith("<!DOCTYPE html") && !$"{email.Subject}{email.Html}{email.Text}".Contains("{{"));
    }

    // The old address learns where the account went without the whole new address, and how to reach support.
    [Fact]
    public async Task SendEmailChangeNoticeEmail_AChange_TellsTheOldAddressTheMaskedNewOne()
    {
        await SendEmailChangeNoticeEmailHandler.HandleAsync(Notice(Language.En), _channel, Cancellation);

        var email = _channel.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject, email.IdempotencyKey).ShouldBe(("ada@example.com", "Your email address was changed", $"email-changed/{UserId}/{CodeId}"));
        email.Text.ShouldContain("Your email address was changed to a***@new.example.");
        email.Text.ShouldNotContain("ada@new.example");
        email.Html.ShouldContain("href=\"mailto:support@starter.example\"");
    }

    [Theory]
    [InlineData("ada@new.example", "a***@new.example")]
    [InlineData("a@b.example", "a***@b.example")]
    [InlineData("no-at-sign", "***")]
    public void MaskEmail_AnAddress_KeepsItsFirstCharacterAndItsDomain(string email, string masked) =>
        MaskedEmail.Of(email).ShouldBe(masked);

    private static VerificationCodeReadyEvent Code(VerificationEmailKind kind, Language language) =>
        new(CodeId, kind, "ada@new.example", language, "381906", new DateTimeOffset(2026, 11, 4, 9, 10, 0, TimeSpan.Zero));

    private static EmailChangeNoticeReadyEvent Notice(Language language) => new(UserId, CodeId, "ada@example.com", "ada@new.example", language);

    private sealed class RecordingEmailChannel : IEmailChannel
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            Sent.Add(email);
            return Task.CompletedTask;
        }
    }
}
