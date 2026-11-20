namespace Notifications.Domain.Notifications;

/// <summary>How a value of a notification's text is stored, and how the read writes it in the reader's language (U2).</summary>
public enum NotificationValueKind
{
    /// <summary>Text as stored: a name that is not a person's, as it was at that moment, such as a tenant's or a plan's.</summary>
    Text,

    /// <summary>A person, stored by user id only (K3). The read names them now; a deleted user reads as "Deleted user" in the language.</summary>
    Person,

    /// <summary>A role: a built-in role in the reader's language, a custom role as the Owner typed it. The value <c>{name}BuiltIn</c> says which.</summary>
    Role,

    /// <summary>A word of a closed list, which the texts translate.</summary>
    Word,

    /// <summary>A key of ControlPlane's entitlement list, written by its name in the language, as the plan screens write it.</summary>
    EntitlementKey,

    /// <summary>An entitlement's value: a number as stored; <c>unlimited</c>, <c>on</c>, <c>off</c> and <c>not_set</c> in the language.</summary>
    Entitlement,

    /// <summary>A moment, stored in ISO 8601, written as a date in the reader's language and time zone.</summary>
    Date,

    /// <summary>
    /// A calendar day, stored in ISO 8601 (<c>2026-12-01</c>), written as a date in the reader's language and in no time zone: the day is the
    /// same for every reader, as a terms version's day is.
    /// </summary>
    Day,

    /// <summary>
    /// A number of days, stored as digits, written with its noun in the form of the number's CLDR plural category in the language.
    /// </summary>
    Days,

    /// <summary>
    /// A number of feature flags, stored as digits, written with its noun in the form of the number's CLDR plural category in the language,
    /// as <see cref="Days"/> is.
    /// </summary>
    FeatureFlags,

    /// <summary>
    /// An amount of money, stored as the whole number of its currency's minor unit and the ISO 4217 code (<c>10000 EUR</c>), written as the
    /// apps' <c>formatMoney</c> writes it in the language: <c>€100</c>, <c>€12.50</c>; a whole amount shows no decimals.
    /// </summary>
    Money,

    /// <summary>
    /// A number of attempts at a payment, stored as digits, written with its noun in the form of the number's CLDR plural category in the
    /// language, as <see cref="Days"/> is.
    /// </summary>
    Attempts,

    /// <summary>
    /// A number of people who take a seat, stored as digits, written with its noun in the form of the number's CLDR plural category in the
    /// language, as <see cref="Days"/> is.
    /// </summary>
    People,

    /// <summary>
    /// A meter of the code, stored by its key, written by its word under <c>notificationWords.meter</c> in the language; a key without a word
    /// reads as the key, as the template ships no meter (step 40c).
    /// </summary>
    Meter,

    /// <summary>A count of a meter's units, stored as digits, written grouped by three in the language, as an amount's whole part is.</summary>
    Count,
}
