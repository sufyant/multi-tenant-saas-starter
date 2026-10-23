using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        // A name is checked when it is given; whether it must be given depends on whether the person is a user, which the handler reads.
        RuleFor(command => command.FirstName).ValidPersonName().When(command => command.FirstName is not null);
        RuleFor(command => command.LastName).ValidPersonName().When(command => command.LastName is not null);
    }
}
