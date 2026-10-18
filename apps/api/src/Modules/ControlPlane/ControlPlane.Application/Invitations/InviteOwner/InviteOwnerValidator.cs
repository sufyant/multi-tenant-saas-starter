using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteOwner;

public sealed class InviteOwnerValidator : AbstractValidator<InviteOwnerCommand>
{
    public InviteOwnerValidator()
    {
        // The same check of an address as a tenant's first owner has. The invitation trims it.
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(Invitation.EmailMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
