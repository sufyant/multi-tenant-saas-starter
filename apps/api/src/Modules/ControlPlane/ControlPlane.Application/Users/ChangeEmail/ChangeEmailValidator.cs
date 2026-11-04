using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.ChangeEmail;

public sealed class ChangeEmailValidator : AbstractValidator<ChangeEmailCommand>
{
    public ChangeEmailValidator() =>
        RuleFor(command => command.Code).NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required));
}
