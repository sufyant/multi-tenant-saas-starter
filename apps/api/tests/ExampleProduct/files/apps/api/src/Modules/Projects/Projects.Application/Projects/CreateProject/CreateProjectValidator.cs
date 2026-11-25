using FluentValidation;
using Projects.Domain.Projects;
using SharedKernel;

namespace Projects.Application.Projects.CreateProject;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator() =>
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumLength(Project.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
}
