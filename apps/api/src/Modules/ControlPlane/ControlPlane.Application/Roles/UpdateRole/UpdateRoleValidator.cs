using ControlPlane.Application.Validation;
using ControlPlane.Domain.Roles;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Roles.UpdateRole;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        // As CreateRoleValidator: the tenant checks the rest.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Role.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Permissions).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
        RuleForEach(command => command.Permissions).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
    }
}
