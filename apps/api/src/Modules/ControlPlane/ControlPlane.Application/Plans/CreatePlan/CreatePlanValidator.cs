using ControlPlane.Application.Validation;
using ControlPlane.Domain.Plans;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Plans.CreatePlan;

public sealed class CreatePlanValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanValidator()
    {
        // The rule states the name's longest length, so the OpenAPI document holds it; the plan still checks the name itself (Evans,
        // Chapter 6). The plan keeps the name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Plan.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Entitlements).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
    }
}
