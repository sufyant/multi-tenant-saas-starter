using ControlPlane.Domain.EmailAddresses;
using System.Text.RegularExpressions;
using ControlPlane.Application.Validation;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants.CreateTenant;

public sealed partial class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        // The rules state the name's longest length and the slug's lengths and shape, so the OpenAPI document holds them; the tenant
        // still checks both itself (Evans, Chapter 6). The tenant keeps the name without the spaces around it, so its length is counted
        // without them.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Slug)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .Length(Tenant.SlugMinLength, Tenant.SlugMaxLength).WithErrorCode(nameof(FieldErrorCode.SlugInvalid))
            .Matches(Slug()).WithErrorCode(nameof(FieldErrorCode.SlugInvalid));
        RuleFor(command => command.OwnerEmail)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(EmailAddress.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }

    // Parts of lowercase ASCII letters and digits, joined by single hyphens. The OpenAPI document writes it as the slug's pattern, so
    // .NET and ECMAScript must read it the same way: no \z and no lookbehind. A $ would not do: in .NET it also matches before a last
    // line break. (?![\s\S]), no character after, is the end of the text in both.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*(?![\s\S])", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();
}
