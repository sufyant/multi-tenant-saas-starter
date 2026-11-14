using ControlPlane.Application.Validation;
using ControlPlane.Domain.TermsVersions;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions.CreateTermsVersion;

public sealed class CreateTermsVersionValidator : AbstractValidator<CreateTermsVersionCommand>
{
    public CreateTermsVersionValidator()
    {
        // The rules state the longest lengths, so the OpenAPI document holds them; the version still checks the link itself (Evans,
        // Chapter 6). The version keeps both without the spaces around them, so their lengths are counted without them.
        RuleFor(command => command.Url)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(TermsVersion.UrlMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Summary).MaximumTrimmedLength(TermsVersion.SummaryMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
