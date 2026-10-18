using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Users;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>A system admin changes a catalog user's names, in the catalog only (A7). It runs without a tenant.</summary>
public sealed record RenameUserCommand(string ActorId, Guid UserId, string FirstName, string LastName);

public sealed class RenameUserValidator : AbstractValidator<RenameUserCommand>
{
    public RenameUserValidator()
    {
        // The user keeps a name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.FirstName)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.LastName)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}

// The command sets a state (I1): the names the user has answer as they are, and nothing is announced. It locks the user row, so two
// changes of the user run one at a time (K6). Every rejection returns before the handler changes anything, because Wolverine commits a
// failed Result too (W7).
public static class RenameUserHandler
{
    public static async Task<(Result<UserView>, UserRenamedEvent?)> HandleAsync(
        RenameUserCommand command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        var renamed = user.Rename(command.FirstName, command.LastName);
        if (!renamed.IsSuccess || !renamed.Value)
        {
            return (renamed.Map(_ => UserView.Of(user, isSystemAdmin)), null);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (UserView.Of(user, isSystemAdmin), new UserRenamedEvent(Guid.CreateVersion7(now), now, user.Id, actor));
    }
}
