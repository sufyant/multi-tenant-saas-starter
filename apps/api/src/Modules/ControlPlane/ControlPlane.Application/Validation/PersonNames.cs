using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Validation;

public static class PersonNames
{
    /// <summary>
    /// A person's first or last name, as ControlPlane keeps it (A7): required, and at most <see cref="User.NameMaxLength"/> characters,
    /// counted without the spaces around it, which the user does not keep. One rule for every request that gives a name: an invitation's
    /// acceptance, a system admin's rename and the user's own.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ValidPersonName<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
}
