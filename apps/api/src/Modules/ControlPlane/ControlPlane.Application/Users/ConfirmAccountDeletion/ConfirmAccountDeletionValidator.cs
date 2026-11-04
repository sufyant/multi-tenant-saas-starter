using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.ConfirmAccountDeletion;

public sealed class ConfirmAccountDeletionValidator : AbstractValidator<ConfirmAccountDeletionCommand>
{
    public ConfirmAccountDeletionValidator() =>
        RuleFor(command => command.Code).NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required));
}
