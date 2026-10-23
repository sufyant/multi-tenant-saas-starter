using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Users.RenameUser;

public sealed class RenameUserValidator : AbstractValidator<RenameUserCommand>
{
    public RenameUserValidator()
    {
        RuleFor(command => command.FirstName).ValidPersonName();
        RuleFor(command => command.LastName).ValidPersonName();
    }
}
