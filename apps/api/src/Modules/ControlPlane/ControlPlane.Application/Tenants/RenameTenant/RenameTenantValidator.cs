using ControlPlane.Application.Validation;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants.RenameTenant;

public sealed class RenameTenantValidator : AbstractValidator<RenameTenantCommand>
{
    public RenameTenantValidator()
    {
        // The rule states the name's longest length, so the OpenAPI document holds it; the tenant still checks the name itself, as at
        // its creation (Evans, Chapter 6). The tenant keeps the name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
