using System.Text.RegularExpressions;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The signed-in user sets their own language and time zone, which follow them to every tenant and device. It runs without a tenant.
/// The time zone is an IANA name, such as <c>Europe/Dublin</c>, or null for "not set".
/// </summary>
public sealed record ChangePreferencesCommand(string UserId, Language Language, string? TimeZone);

/// <summary>The user's preferences, as the user keeps them.</summary>
public sealed record PreferencesDetails(Language Language, string? TimeZone);

public sealed partial class ChangePreferencesValidator : AbstractValidator<ChangePreferencesCommand>
{
    public ChangePreferencesValidator()
    {
        // The API holds no time zone data and never interprets a time zone (invariant globalization mode, section 7): it checks the
        // shape of the name only. The web offers the list.
        RuleFor(command => command.TimeZone!)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(User.TimeZoneMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .Matches(IanaName()).WithErrorCode(nameof(FieldErrorCode.TimeZoneInvalid))
            .When(command => command.TimeZone is not null);
    }

    // Parts of ASCII letters, digits, "_", "-" and "+", joined by single slashes, as the IANA database names its time zones
    // (IANA, "Theory and pragmatics of the tz code and data", "Names of timezones"). \z ends the match at the very end: $ would let a
    // line break through.
    [GeneratedRegex(@"^[A-Za-z0-9_+\-]+(/[A-Za-z0-9_+\-]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IanaName();
}

// The command sets a state (I1): the values the user has answer as they are, and nothing is written or announced. It locks the user row,
// so two changes of the user run one at a time (K6), and a deletion that commits while it waits is refused (A9). Every rejection
// returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class ChangePreferencesHandler
{
    public static async Task<(Result<PreferencesDetails>, UserPreferencesChangedEvent?)> HandleAsync(
        ChangePreferencesCommand command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // A person becomes a catalog user only through an invitation or as the first system admin, as GetMe answers.
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var changed = user.ChangePreferences(command.Language, command.TimeZone);
        if (!changed.IsSuccess || !changed.Value)
        {
            return (changed.Map(_ => new PreferencesDetails(user.Language, user.TimeZone)), null);
        }

        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (new PreferencesDetails(user.Language, user.TimeZone), new UserPreferencesChangedEvent(Guid.CreateVersion7(now), now, user.Id));
    }
}
