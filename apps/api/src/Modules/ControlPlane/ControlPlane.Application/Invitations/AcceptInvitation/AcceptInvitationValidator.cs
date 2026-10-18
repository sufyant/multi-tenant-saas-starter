using ControlPlane.Application.Validation;
using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        // A name is checked when it is given; whether it must be given depends on whether the person is a user, which the handler reads. The user keeps a
        // name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.FirstName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(command => command.FirstName is not null);
        RuleFor(command => command.LastName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(command => command.LastName is not null);
    }
}
