using ControlPlane.Application.Validation;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants.SetEntitlementOverride;

public sealed class SetEntitlementOverrideValidator : AbstractValidator<SetEntitlementOverrideCommand>
{
    public SetEntitlementOverrideValidator()
    {
        RuleFor(command => command.Value).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));

        // The override keeps the reason without the spaces around it, so its length is counted without them, as a suspension's is.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .Must(reason => reason.Trim().Length > 0).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(EntitlementOverride.ReasonMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
