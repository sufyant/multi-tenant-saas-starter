namespace Notifications.Application.Email;

// A number of days in each language's plural forms (Unicode CLDR, "Language Plural Rules"), for the emails that say one: the invitation's
// lifetime and the time left before a tenant's purge. English and Turkish keep their forms in their own texts.
internal static class Days
{
    // Arabic counts days in six forms: one, two, a few (the last two digits 3 to 10), many (11 to 99), and the rest.
    public static string InArabic(int days) => (days, days % 100) switch
    {
        (1, _) => "يوم واحد",
        (2, _) => "يومين",
        (_, >= 3 and <= 10) => $"{days} أيام",
        (_, >= 11) => $"{days} يومًا",
        _ => $"{days} يوم",
    };
}
