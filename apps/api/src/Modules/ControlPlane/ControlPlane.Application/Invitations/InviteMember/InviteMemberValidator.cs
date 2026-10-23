using ControlPlane.Domain.EmailAddresses;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        // The same check of an address as a tenant's first owner has. The invitation trims it.
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(EmailAddress.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
