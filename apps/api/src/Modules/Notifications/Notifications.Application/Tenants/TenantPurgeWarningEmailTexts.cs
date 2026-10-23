using System.Globalization;
using Notifications.Application.Email;
using SharedKernel;

namespace Notifications.Application.Tenants;

// The texts of the email that warns an Owner of a closed tenant's purge. Each language is one block here, as the invitation's texts are; a
// new language adds its block. The email is plain text. The image has no time zone data, so the moment of the purge is written in UTC, with
// Western digits, in every language (section 7). The days are the whole days nearest to the time left, at least one, with each language's
// plural forms.
internal static class TenantPurgeWarningEmailTexts
{
    private static readonly Dictionary<Language, Texts> ByLanguage = new()
    {
        [Language.En] = new(
            Subject: (tenant, days) => $"{tenant} will be deleted in {EnglishDays(days)}",
            Warning: (tenant, days, date, clock) =>
                $"Your organisation {tenant} is closed. It will be deleted with all its data in {EnglishDays(days)}, on {date} at {clock} UTC. "
                + "After that, nobody can reopen it.",
            Keep: "To keep it, ask the platform's administrators to reopen it before then."),

        // Turkish adds its case endings to a name by the name's last vowel, so the tenant's name stands before "adlı", which takes them.
        [Language.Tr] = new(
            Subject: (tenant, days) => $"{tenant} adlı kuruluş {days} gün içinde silinecek",
            Warning: (tenant, days, date, clock) =>
                $"{tenant} adlı kuruluşunuz kapatıldı. Kuruluş tüm verileriyle birlikte {days} gün içinde, {date} tarihinde {clock} UTC'de silinecek. "
                + "Bundan sonra kimse onu yeniden açamaz.",
            Keep: "Kuruluşu korumak için bu tarihten önce platform yöneticilerinden onu yeniden açmalarını isteyin."),

        [Language.Ar] = new(
            Subject: (tenant, days) => $"سيتم حذف {tenant} خلال {Days.InArabic(days)}",
            Warning: (tenant, days, date, clock) =>
                $"تم إغلاق مؤسستك {tenant}. سيتم حذفها مع جميع بياناتها خلال {Days.InArabic(days)}، في {date} الساعة {clock} بالتوقيت العالمي (UTC). "
                + "بعد ذلك لا يمكن لأحد إعادة فتحها.",
            Keep: "للاحتفاظ بها، اطلب من مسؤولي المنصة إعادة فتحها قبل ذلك."),
    };

    public static EmailMessage Write(string to, string tenantName, Language language, DateTimeOffset purgesAt, TimeSpan timeLeft, Guid tenantId, Guid userId)
    {
        var texts = ByLanguage[language];
        var days = Math.Max(1, (int)Math.Round(timeLeft.TotalDays, MidpointRounding.AwayFromZero));
        var utc = purgesAt.ToUniversalTime();
        var date = utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var clock = utc.ToString("HH:mm", CultureInfo.InvariantCulture);
        var text = $"{texts.Warning(tenantName, days, date, clock)}\n\n{texts.Keep}";

        // The key is the Owner's warning of this purge, so an event that arrives twice sends one email; a tenant closed again purges at
        // another moment, so its warning is a new email.
        return new EmailMessage(to, texts.Subject(tenantName, days), text, $"purge-warning/{tenantId}/{userId}/{purgesAt.UtcTicks}");
    }

    private static string EnglishDays(int days) => days == 1 ? "1 day" : $"{days} days";

    private sealed record Texts(Func<string, int, string> Subject, Func<string, int, string, string, string> Warning, string Keep);
}
