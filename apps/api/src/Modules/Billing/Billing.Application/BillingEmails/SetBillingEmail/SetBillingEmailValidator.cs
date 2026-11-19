using Billing.Domain.BillingEmails;
using FluentValidation;
using SharedKernel;

namespace Billing.Application.BillingEmails.SetBillingEmail;

public sealed class SetBillingEmailValidator : AbstractValidator<SetBillingEmailCommand>
{
    public SetBillingEmailValidator()
    {
        // The same check of an address as an invitation's; none removes the billing email.
        RuleFor(command => command.Address)
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(BillingEmail.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(command => command.Address is not null);
    }
}
