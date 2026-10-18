using FluentValidation;
using FluentValidation.Validators;

namespace ControlPlane.Application;

/// <summary>
/// A text that the catalog keeps without the spaces around it is at most <see cref="Max"/> characters, counted without them. The rule
/// is a length rule (<see cref="ILengthValidator"/>), so the OpenAPI document reads its limits (N11). A null value passes: another rule
/// says whether the value is required.
/// </summary>
public sealed class TrimmedMaximumLengthValidator<T>(int max) : PropertyValidator<T, string?>, ILengthValidator
{
    public override string Name => "TrimmedMaximumLengthValidator";

    public int Min => 0;

    public int Max => max;

    public override bool IsValid(ValidationContext<T> context, string? value) => value is null || value.Trim().Length <= max;

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' is too long, not counting the spaces around it.";
}

public static class TrimmedLength
{
    /// <summary>The value is at most <paramref name="max"/> characters, not counting the spaces around it (FluentValidation, "Custom Validators").</summary>
    public static IRuleBuilderOptions<T, string?> MaximumTrimmedLength<T>(this IRuleBuilder<T, string?> rule, int max) =>
        rule.SetValidator(new TrimmedMaximumLengthValidator<T>(max));
}
