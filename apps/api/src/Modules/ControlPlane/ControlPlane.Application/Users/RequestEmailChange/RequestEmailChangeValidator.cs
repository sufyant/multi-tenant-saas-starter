using ControlPlane.Domain.EmailAddresses;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.RequestEmailChange;

public sealed class RequestEmailChangeValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeValidator()
    {
        // The same check of an address as an invitation's. The address takes its one form in the handler.
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(EmailAddress.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
