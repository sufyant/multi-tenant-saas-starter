using ControlPlane.Application.Validation;
using ControlPlane.Domain.TermsVersions;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions.UpdateTermsVersion;

public sealed class UpdateTermsVersionValidator : AbstractValidator<UpdateTermsVersionCommand>
{
    public UpdateTermsVersionValidator()
    {
        // As CreateTermsVersionValidator: the document holds the longest lengths, counted without the spaces around a value.
        RuleFor(command => command.Url)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(TermsVersion.UrlMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Summary).MaximumTrimmedLength(TermsVersion.SummaryMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
