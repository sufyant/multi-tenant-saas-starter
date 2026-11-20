using ControlPlane.Application.Validation;
using FluentValidation;

namespace ControlPlane.Application.Tenants.CreateMyTenant;

public sealed class CreateMyTenantValidator : AbstractValidator<CreateMyTenantCommand>
{
    public CreateMyTenantValidator()
    {
        RuleFor(command => command.Name).ValidTenantName();
        RuleFor(command => command.Slug).ValidTenantSlug();
    }
}
