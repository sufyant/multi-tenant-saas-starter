using System.Globalization;

namespace Notifications.Application.Email;

/// <summary>
/// How a language writes a date and a moment in an email, from <c>packages/emails</c>: the one formatter of both emails. The image runs
/// in invariant globalization mode and has no ICU (ARCHITECTURE.md, section 7), so the month names and the patterns come with the
/// emails, as the web writes them (F18 in the web app's architecture: "1 Nov 2026", "1 Kas 2026", "1 نوفمبر 2026"). Digits are Western.
/// A moment is written in a time zone, followed by the zone's label as the web names a zone it has no name for: the city of the IANA name
/// and the offset at that moment, <c>Istanbul (UTC+3)</c>. The image carries the time zone data (section 7). Without a zone, or with one
/// the data does not know, the moment is in UTC, which the language writes in its own words.
/// </summary>
/// <param name="Months">The twelve month names, January first.</param>
/// <param name="Date">The pattern of a date, of <c>{day}</c>, <c>{month}</c> and <c>{year}</c>.</param>
/// <param name="Moment">The pattern of a moment, of <c>{date}</c>, <c>{time}</c> and <c>{zone}</c>.</param>
/// <param name="Zone">The pattern of a zone's label, of <c>{city}</c> and <c>{offset}</c>.</param>
/// <param name="Utc">The label of UTC.</param>
internal sealed record EmailDates(IReadOnlyList<string> Months, string Date, string Moment, string Zone, string Utc)
{
    /// <summary>The day and the moment of an instant in the time zone, an IANA name or null.</summary>
    public (string Day, string Moment) Write(DateTimeOffset at, string? timeZone)
    {
        var zone = timeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found) ? found : null;
        var local = zone is null ? at.ToUniversalTime() : TimeZoneInfo.ConvertTime(at, zone);
        var day = Date
            .Replace("{day}", local.Day.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{month}", Months[local.Month - 1], StringComparison.Ordinal)
            .Replace("{year}", local.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var label = zone is null
            ? Utc
            : Zone.Replace("{city}", CityOf(timeZone!), StringComparison.Ordinal).Replace("{offset}", UtcOffset(local.Offset), StringComparison.Ordinal);
        var moment = Moment
            .Replace("{date}", day, StringComparison.Ordinal)
            .Replace("{time}", local.ToString("HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{zone}", label, StringComparison.Ordinal);
        return (day, moment);
    }

    // The city of an IANA zone, as the web writes it: America/Argentina/Buenos_Aires gives Buenos Aires. Without ICU it stays in English.
    private static string CityOf(string timeZone) => timeZone[(timeZone.LastIndexOf('/') + 1)..].Replace('_', ' ');

    // The offset as the web writes it: UTC+1, UTC+5:30, UTC-4.
    private static string UtcOffset(TimeSpan offset) =>
        $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours)}{(offset.Minutes == 0 ? "" : $":{Math.Abs(offset.Minutes):00}")}";
}
