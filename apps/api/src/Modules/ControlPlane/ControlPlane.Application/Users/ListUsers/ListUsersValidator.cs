using ControlPlane.Domain.EmailAddresses;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        // The same check of an address as the other email inputs have. The filter is optional, so only a given address is checked.
        RuleFor(query => query.Email)
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(EmailAddress.MaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(query => query.Email is not null);
    }
}
