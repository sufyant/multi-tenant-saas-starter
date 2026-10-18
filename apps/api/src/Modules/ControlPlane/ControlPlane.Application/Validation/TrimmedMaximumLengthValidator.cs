using FluentValidation;
using FluentValidation.Validators;

namespace ControlPlane.Application.Validation;

/// <summary>
/// A text that ControlPlane keeps without the spaces around it is at most <see cref="Max"/> characters, counted without them. The rule
/// is a length rule (<see cref="ILengthValidator"/>), so the OpenAPI document reads its limits (N11m). A null value passes: another rule
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
