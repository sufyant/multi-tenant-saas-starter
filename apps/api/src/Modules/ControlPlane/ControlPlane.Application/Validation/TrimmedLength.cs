using FluentValidation;

namespace ControlPlane.Application.Validation;

public static class TrimmedLength
{
    /// <summary>The value is at most <paramref name="max"/> characters, not counting the spaces around it (FluentValidation, "Custom Validators").</summary>
    public static IRuleBuilderOptions<T, string?> MaximumTrimmedLength<T>(this IRuleBuilder<T, string?> rule, int max) =>
        rule.SetValidator(new TrimmedMaximumLengthValidator<T>(max));
}
