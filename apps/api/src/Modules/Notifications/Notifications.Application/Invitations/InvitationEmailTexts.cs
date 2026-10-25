using Notifications.Application.Email;
using SharedKernel;

namespace Notifications.Application.Invitations;

// The texts of the two invitation emails, the owner's and the member's, which say the same. Each language is one block here, so a
// reviewer reads all of its texts in one place; a new language adds its block. The email is plain text. It shows no date or time, so it
// needs no time zone: it says how long the link works, in whole days, with each language's plural forms (Unicode CLDR, "Language Plural
// Rules").
internal static class InvitationEmailTexts
{
    private static readonly Dictionary<Language, Texts> ByLanguage = new()
    {
        [Language.En] = new(
            Subject: tenant => $"You are invited to {tenant}",
            Invitation: tenant => $"You have been invited to join {tenant}. Open this link to accept the invitation:",
            Lifetime: days => days == 1 ? "The link works for 1 day." : $"The link works for {days} days."),

        // Turkish adds its case endings to a name by the name's last vowel, so the tenant's name stands before "adlı", which takes them.
        [Language.Tr] = new(
            Subject: tenant => $"{tenant} adlı kuruluşa davet edildiniz",
            Invitation: tenant => $"{tenant} adlı kuruluşa katılmaya davet edildiniz. Daveti kabul etmek için bu bağlantıyı açın:",
            Lifetime: days => $"Bağlantı {days} gün geçerlidir."),

        [Language.Ar] = new(
            Subject: tenant => $"دعوة للانضمام إلى {tenant}",
            Invitation: tenant => $"تمت دعوتك للانضمام إلى {tenant}. افتح هذا الرابط لقبول الدعوة:",
            Lifetime: days => $"الرابط صالح لمدة {Days.InArabic(days)}."),
    };

    public static EmailMessage Write(string to, string tenantName, Uri link, Language language, TimeSpan lifetime, Guid invitationId)
    {
        var texts = ByLanguage[language];
        var text = $"{texts.Invitation(tenantName)}\n\n{link}\n\n{texts.Lifetime((int)lifetime.TotalDays)}";

        // The idempotency key is the invitation's, so an event that arrives twice sends one email.
        return new EmailMessage(to, texts.Subject(tenantName), "", text, $"invite/{invitationId}");
    }

    private sealed record Texts(Func<string, string> Subject, Func<string, string> Invitation, Func<int, string> Lifetime);
}
