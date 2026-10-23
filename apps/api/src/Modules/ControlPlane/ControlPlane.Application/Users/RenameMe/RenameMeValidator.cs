using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Users.RenameMe;

public sealed class RenameMeValidator : AbstractValidator<RenameMeCommand>
{
    public RenameMeValidator()
    {
        RuleFor(command => command.FirstName).ValidPersonName();
        RuleFor(command => command.LastName).ValidPersonName();
    }
}
