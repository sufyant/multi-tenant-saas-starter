using System.Globalization;
using Notifications.Application.Email;
using SharedKernel;

namespace Notifications.Application.Tenants;

// The texts of the email that warns an Owner of a closed tenant's purge. Each language is one block here, as the invitation's texts are; a
// new language adds its block. The email is plain text. The moment of the purge is written in the Owner's own time zone, with the zone's
// city and its offset at that moment, as the UI names a zone it has no name for: Istanbul (UTC+3) (F18 in the web app's architecture). The
// image carries the time zone data (section 7). Without a zone, or with one the data does not know, the moment is in UTC. Digits are
// Western in every language. The days are the whole days nearest to the time left, at least one, with each language's plural forms.
internal static class TenantPurgeWarningEmailTexts
{
    private static readonly Dictionary<Language, Texts> ByLanguage = new()
    {
        [Language.En] = new(
            Subject: (tenant, days) => $"{tenant} will be deleted in {EnglishDays(days)}",
            Warning: (tenant, days, date, clock, zone) =>
                $"Your organisation {tenant} is closed. It will be deleted with all its data in {EnglishDays(days)}, "
                + $"on {date} at {clock} {zone ?? "UTC"}. "
                + "After that, nobody can reopen it.",
            Keep: "To keep it, ask the platform's administrators to reopen it before then."),

        // Turkish adds its case endings to a name by the name's last vowel, so the tenant's name stands before "adlı", which takes them.
        [Language.Tr] = new(
            Subject: (tenant, days) => $"{tenant} adlı kuruluş {days} gün içinde silinecek",
            Warning: (tenant, days, date, clock, zone) =>
                $"{tenant} adlı kuruluşunuz kapatıldı. Kuruluş tüm verileriyle birlikte {days} gün içinde, "
                + (zone is null ? $"{date} tarihinde {clock} UTC'de silinecek. " : $"{date} tarihinde {zone} saatiyle {clock} itibarıyla silinecek. ")
                + "Bundan sonra kimse onu yeniden açamaz.",
            Keep: "Kuruluşu korumak için bu tarihten önce platform yöneticilerinden onu yeniden açmalarını isteyin."),

        [Language.Ar] = new(
            Subject: (tenant, days) => $"سيتم حذف {tenant} خلال {Days.InArabic(days)}",
            Warning: (tenant, days, date, clock, zone) =>
                $"تم إغلاق مؤسستك {tenant}. سيتم حذفها مع جميع بياناتها خلال {Days.InArabic(days)}، في {date} الساعة {clock} "
                + (zone is null ? "بالتوقيت العالمي (UTC). " : $"بتوقيت {zone}. ")
                + "بعد ذلك لا يمكن لأحد إعادة فتحها.",
            Keep: "للاحتفاظ بها، اطلب من مسؤولي المنصة إعادة فتحها قبل ذلك."),
    };

    public static EmailMessage Write(
        string to, string tenantName, Language language, DateTimeOffset purgesAt, string? timeZone, TimeSpan timeLeft, Guid tenantId, Guid userId)
    {
        var texts = ByLanguage[language];
        var days = Math.Max(1, (int)Math.Round(timeLeft.TotalDays, MidpointRounding.AwayFromZero));
        var zone = timeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found) ? found : null;
        var moment = zone is null ? purgesAt.ToUniversalTime() : TimeZoneInfo.ConvertTime(purgesAt, zone);
        var date = moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var clock = moment.ToString("HH:mm", CultureInfo.InvariantCulture);
        var label = zone is null ? null : $"{CityOf(timeZone!)} ({UtcOffset(moment.Offset)})";
        var text = $"{texts.Warning(tenantName, days, date, clock, label)}\n\n{texts.Keep}";

        // The key is the Owner's warning of this purge, so an event that arrives twice sends one email; a tenant closed again purges at
        // another moment, so its warning is a new email.
        return new EmailMessage(to, texts.Subject(tenantName, days), "", text, $"purge-warning/{tenantId}/{userId}/{purgesAt.UtcTicks}");
    }

    private static string EnglishDays(int days) => days == 1 ? "1 day" : $"{days} days";

    // The city of an IANA zone, as the UI writes it: America/Argentina/Buenos_Aires gives Buenos Aires.
    private static string CityOf(string timeZone) => timeZone[(timeZone.LastIndexOf('/') + 1)..].Replace('_', ' ');

    // The offset as the UI writes it: UTC+1, UTC+5:30, UTC-4.
    private static string UtcOffset(TimeSpan offset) =>
        $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours)}{(offset.Minutes == 0 ? "" : $":{Math.Abs(offset.Minutes):00}")}";

    // The zone is the label of the Owner's zone, or null for UTC: each language writes UTC in its own words.
    private sealed record Texts(Func<string, int, string> Subject, Func<string, int, string, string, string?, string> Warning, string Keep);
}
