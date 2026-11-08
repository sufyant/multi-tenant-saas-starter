using ControlPlane.Application.Validation;
using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.SuspendUser;

// The rule of SuspendTenant's reason (the maintainer's decision of 2026-11-08).
public sealed class SuspendUserValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserValidator()
    {
        // The user keeps the reason without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.SuspensionReasonMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
