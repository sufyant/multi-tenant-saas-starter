using ControlPlane.Application.Validation;
using ControlPlane.Domain.Plans;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Plans.UpdatePlan;

public sealed class UpdatePlanValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanValidator()
    {
        // As CreatePlanValidator: the document holds the name's longest length, counted without the spaces around it.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Plan.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Entitlements).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
    }
}
