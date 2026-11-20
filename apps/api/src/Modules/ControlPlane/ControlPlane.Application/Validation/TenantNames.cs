using System.Text.RegularExpressions;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Validation;

/// <summary>
/// A tenant's name and slug, with one rule for every request that creates a tenant: a system admin's and a user's own (step 40d). The rules
/// state the name's longest length and the slug's lengths and shape, so the OpenAPI document holds them; the tenant still checks both
/// itself (Evans, Chapter 6). A reserved slug is refused with a code of its own, so a form can say why; the document lists the reserved
/// slugs as an enum of their own. The tenant keeps the name without the spaces around it, so its length is counted without them.
/// </summary>
public static partial class TenantNames
{
    public static IRuleBuilderOptions<T, string?> ValidTenantName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));

    public static IRuleBuilderOptions<T, string?> ValidTenantSlug<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .Length(Tenant.SlugMinLength, Tenant.SlugMaxLength).WithErrorCode(nameof(FieldErrorCode.SlugInvalid))
            .Matches(Slug()).WithErrorCode(nameof(FieldErrorCode.SlugInvalid))
            .Must(slug => !ReservedSlugs.Contains(slug)).WithErrorCode(nameof(FieldErrorCode.SlugReserved));

    // Parts of lowercase ASCII letters and digits, joined by single hyphens. The OpenAPI document writes it as the slug's pattern, so
    // .NET and ECMAScript must read it the same way: no \z and no lookbehind. A $ would not do: in .NET it also matches before a last
    // line break. (?![\s\S]), no character after, is the end of the text in both.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*(?![\s\S])", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();
}
