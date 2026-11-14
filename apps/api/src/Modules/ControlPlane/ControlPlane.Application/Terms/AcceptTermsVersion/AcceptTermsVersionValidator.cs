using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Terms.AcceptTermsVersion;

public sealed class AcceptTermsVersionValidator : AbstractValidator<AcceptTermsVersionCommand>
{
    public AcceptTermsVersionValidator() => RuleFor(command => command.Url).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
}
