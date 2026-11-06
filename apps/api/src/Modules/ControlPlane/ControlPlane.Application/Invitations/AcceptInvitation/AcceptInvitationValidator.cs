using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        // The name is checked when it is given; whether it must be given depends on whether the person is a user, which the handler reads.
        RuleFor(command => command.Name).ValidPersonName().When(command => command.Name is not null);
    }
}
