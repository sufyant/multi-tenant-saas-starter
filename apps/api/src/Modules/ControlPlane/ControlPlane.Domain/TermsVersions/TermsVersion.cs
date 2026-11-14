using SharedKernel;

namespace ControlPlane.Domain.TermsVersions;

/// <summary>
/// A version of the terms of use: the day it takes effect, the link to its text, an optional summary of what changed, and whether a person
/// must accept it. The text lives outside the template, with the brand's site; the template keeps the link only. A version takes effect at
/// the start of its day in UTC. Its state follows from the days of all versions (<see cref="TermsTimeline"/>): a version whose day has not
/// come is scheduled, the latest whose day has come is in force, an older one is replaced. Only a scheduled version changes, and only a
/// system admin changes it: they edit it or cancel it. A version in force never changes. Once someone accepted a version, its link and
/// whether it requires acceptance lock, because an acceptance proves which text the person agreed to (GDPR, Article 7(1)); its day and
/// summary stay editable.
/// </summary>
internal sealed class TermsVersion
{
    /// <summary>The longest link the API keeps, as browsers and CDNs keep a URL of this length.</summary>
    public const int UrlMaxLength = 2048;

    public const int SummaryMaxLength = 500;

    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.TermsVersionNotFound, "The terms version was not found.");

    public static readonly Error NotScheduled = Error.Conflict(
        ControlPlaneErrorCodes.TermsVersionNotScheduled, "Only a scheduled version changes: a version in force, replaced or cancelled never does.");

    private TermsVersion(Guid id, DateOnly effectiveDate, string url, string? summary, bool requiresAcceptance, bool sendsEmail, Guid createdBy, DateTimeOffset createdAt)
    {
        Id = id;
        EffectiveDate = effectiveDate;
        Url = url;
        Summary = summary;
        RequiresAcceptance = requiresAcceptance;
        SendsEmail = sendsEmail;
        EmailSentAt = sendsEmail ? createdAt : null;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The day the version takes effect, from 00:00 UTC.</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>The absolute <c>https</c> link to the version's text.</summary>
    public string Url { get; private set; }

    /// <summary>What changed, as the system admin wrote it, without the spaces around it; none when they wrote nothing.</summary>
    public string? Summary { get; private set; }

    /// <summary>Whether a person must accept the version; otherwise a notice tells them of it.</summary>
    public bool RequiresAcceptance { get; private set; }

    /// <summary>
    /// Whether the version emails every user who may accept, and a version that requires no acceptance shows its notice on Home. A small
    /// change, such as a typo, does neither: the link only opens the new text (the maintainer's decision of 2026-11-14). The email goes
    /// once: at the creation when it is on, or at the edit that turns it on while the version is scheduled. Once it went, it stays on.
    /// </summary>
    public bool SendsEmail { get; private set; }

    /// <summary>When the version's email went to the users: its creation, or the edit that turned it on; none while it sends none.</summary>
    public DateTimeOffset? EmailSentAt { get; private set; }

    /// <summary>The user id of the system admin who created the version.</summary>
    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When a system admin cancelled the version before it took effect; none for a version that was not cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>The user id of the system admin who cancelled the version.</summary>
    public Guid? CancelledBy { get; private set; }

    /// <summary>The values a system admin gives the version.</summary>
    public TermsVersionValues Values => new(EffectiveDate, Url, Summary, RequiresAcceptance, SendsEmail);

    /// <summary>The day in UTC of a moment, the day a version's effective date is compared with.</summary>
    public static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    /// <summary>
    /// A new version, created at this moment. Its day is today or later, its link an absolute <c>https</c> URL, and no other version is
    /// scheduled. Every rejection comes first.
    /// </summary>
    public static Result<TermsVersion> Create(
        Guid id,
        DateOnly effectiveDate,
        string url,
        string? summary,
        bool requiresAcceptance,
        bool sendsEmail,
        Guid createdBy,
        bool anotherScheduled,
        DateTimeOffset now)
    {
        var checkedValues = Check(effectiveDate, url, DayOf(now));
        if (!checkedValues.IsSuccess)
        {
            return checkedValues.Error;
        }

        if (anotherScheduled)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TermsVersionScheduledExists, "Another version is scheduled. Edit or cancel it first.");
        }

        return new TermsVersion(id, effectiveDate, url.Trim(), Normalize(summary), requiresAcceptance, sendsEmail, createdBy, now);
    }

    /// <summary>Whether the version waits for its day on this day: it was not cancelled, and its day has not come.</summary>
    public bool IsScheduledOn(DateOnly today) => CancelledAt is null && EffectiveDate > today;

    /// <summary>
    /// Gives the scheduled version these values, under the rules of <see cref="Create"/>. A version someone accepted keeps its link and
    /// whether it requires acceptance. A version whose email went keeps it on; one without may turn it on, and the email then goes. Returns
    /// the values before; the values it has change nothing (I1). Every rejection comes first.
    /// </summary>
    public Result<TermsVersionValues> Edit(
        DateOnly effectiveDate,
        string url,
        string? summary,
        bool requiresAcceptance,
        bool sendsEmail,
        bool accepted,
        DateTimeOffset now)
    {
        if (!IsScheduledOn(DayOf(now)))
        {
            return NotScheduled;
        }

        var checkedValues = Check(effectiveDate, url, DayOf(now));
        if (!checkedValues.IsSuccess)
        {
            return checkedValues.Error;
        }

        var trimmed = url.Trim();
        if (accepted && (trimmed != Url || requiresAcceptance != RequiresAcceptance))
        {
            return Error.Conflict(
                ControlPlaneErrorCodes.TermsVersionLocked,
                "Someone accepted this version, so its link and whether it requires acceptance stay. Cancel it and create a new version instead.");
        }

        if (EmailSentAt is not null && !sendsEmail)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TermsVersionEmailSent, "The email of this version went to the users already, so it stays on.");
        }

        var before = Values;
        EffectiveDate = effectiveDate;
        Url = trimmed;
        Summary = Normalize(summary);
        RequiresAcceptance = requiresAcceptance;
        SendsEmail = sendsEmail;
        EmailSentAt ??= sendsEmail ? now : null;
        return before;
    }

    /// <summary>
    /// The system admin cancels the scheduled version at this moment. Its acceptances stay with it. A cancelled version is unchanged; a
    /// version in force or replaced is refused.
    /// </summary>
    public Result<StateChange> Cancel(Guid cancelledBy, DateTimeOffset now)
    {
        if (CancelledAt is not null)
        {
            return StateChange.Unchanged;
        }

        if (!IsScheduledOn(DayOf(now)))
        {
            return NotScheduled;
        }

        CancelledAt = now;
        CancelledBy = cancelledBy;
        return StateChange.Changed;
    }

    private static Result Check(DateOnly effectiveDate, string url, DateOnly today)
    {
        if (effectiveDate < today)
        {
            return Error.Validation(ControlPlaneErrorCodes.TermsVersionEffectiveDatePast, "A version takes effect today or later, never in the past.");
        }

        var trimmed = url.Trim();
        if (trimmed.Length > UrlMaxLength
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(link.Host))
        {
            return Error.Validation(ControlPlaneErrorCodes.TermsVersionUrlInvalid, "A version's link is an absolute https URL.");
        }

        return Result.Success();
    }

    private static string? Normalize(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
}
