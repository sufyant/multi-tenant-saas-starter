using System.Text.RegularExpressions;
using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.ChangePreferences;

public sealed partial class ChangePreferencesValidator : AbstractValidator<ChangePreferencesCommand>
{
    public ChangePreferencesValidator()
    {
        // The API checks the shape of the name only. The web offers the list from the browser's time zone data, which can be newer or
        // older than the image's (section 7). The purge warning writes a zone the image does not know in UTC.
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
