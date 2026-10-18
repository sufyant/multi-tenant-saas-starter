using ControlPlane.Application.Validation;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants.SuspendTenant;

public sealed class SuspendTenantValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantValidator()
    {
        // The tenant keeps the reason without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.SuspensionReasonMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
