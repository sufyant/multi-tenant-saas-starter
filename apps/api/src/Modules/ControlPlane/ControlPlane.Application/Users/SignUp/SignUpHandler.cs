using ControlPlane.Application.TermsVersions;
using ControlPlane.Application.Users.GetMe;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.TermsVersions;
using ControlPlane.Domain.Users;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.SignUp;

// The path of AcceptInvitation for a person who is not a user yet, without a tenant (A7): the identity provider id is locked first, so a
// second sign-up of the person waits and finds the first one's user (K6f); the address is checked under its own lock; the terms the person
// ticked are the version in force. Rejections come first (W7).
public static class SignUpHandler
{
    public static async Task<(Result<MeView>, OutgoingMessages)> HandleAsync(
        SignUpCommand command,
        SignUpOptions signUp,
        IUserRepository users,
        IUserQueries queries,
        ITermsVersionRepository terms,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!signUp.Enabled)
        {
            return (Errors.SignUpDisabled, []);
        }

        if (await users.FindUserForNoKeyUpdateAsync(command.ActorId, cancellationToken) is not null)
        {
            return (await queries.FindMeAsync(command.ActorId, cancellationToken) ?? throw new InvalidOperationException("The user was found under its lock."), []);
        }

        // The identity provider's addresses enter here, in their one form (EmailAddress). The identity provider verifies the address at
        // sign-up (.clerk/config.json), so a new account has one.
        if (EmailAddress.AllOf(command.VerifiedEmails) is not [var email, ..])
        {
            return (Errors.UserEmailUnverified, []);
        }

        if (await users.IsEmailTakenAsync(email, Guid.Empty, cancellationToken))
        {
            return (Errors.UserEmailTaken, []);
        }

        var now = time.GetUtcNow();
        var termsVersion = command.TermsVersionId is { } termsVersionId
            ? await TermsAcceptances.FindAcceptableAsync(terms, termsVersionId, url: null, inForceOnly: true, TermsVersion.DayOf(now), cancellationToken)
            : null;
        if (termsVersion is { IsSuccess: false })
        {
            return (termsVersion.Error, []);
        }

        var user = User.SignUp(Guid.CreateVersion7(now), command.ActorId, email, command.Language, now);
        users.Add(user);

        // GDPR, Article 7(1): the acceptance is the record of the text the person agreed to. The new user's row is saved first, for the
        // acceptance's key.
        if (termsVersion is { Value: { } accepting })
        {
            await terms.SaveChangesAsync(cancellationToken);
            await terms.AcceptAsync(new TermsAcceptance(accepting.Id, user.Id, now), cancellationToken);
        }

        return (
            new MeView(user.Id, user.Email, user.Name, IsSystemAdmin: false, user.Language, user.TimeZone, CreatedTenantCount: 0),
            [new UserSignedUpEvent(Guid.CreateVersion7(now), now, user.Id)]);
    }
}
