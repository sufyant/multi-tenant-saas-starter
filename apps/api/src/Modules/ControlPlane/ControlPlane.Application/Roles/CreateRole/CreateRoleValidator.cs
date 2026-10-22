using ControlPlane.Application.Validation;
using ControlPlane.Domain.Roles;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Roles.CreateRole;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        // The rule states the name's longest length, so the OpenAPI document holds it; the tenant still checks the name and the
        // permissions itself (Evans, Chapter 6). The role keeps its name without the spaces around it.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Role.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Permissions).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
        RuleForEach(command => command.Permissions).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
    }
}
