using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Application.Validation;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(command => command.Name).ValidTenantName();
        RuleFor(command => command.Slug).ValidTenantSlug();
        RuleFor(command => command.OwnerEmail)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(EmailAddress.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}
