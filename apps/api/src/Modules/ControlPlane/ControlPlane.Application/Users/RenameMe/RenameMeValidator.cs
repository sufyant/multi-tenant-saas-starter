using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Users.RenameMe;

public sealed class RenameMeValidator : AbstractValidator<RenameMeCommand>
{
    public RenameMeValidator()
    {
        RuleFor(command => command.Name).ValidPersonName();
    }
}
