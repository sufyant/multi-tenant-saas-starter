using ControlPlane.Application.Terms.GetMyTerms;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Terms;
using SharedKernel;

namespace ControlPlane.Application.Terms.DismissTermsNotice;

// The dismissal is the person's own view, as a notification's read state is (U7): it accepts nothing, and a version that requires
// acceptance still asks for it. A second dismissal records nothing more (I1). It answers what the person must do next.
public static class DismissTermsNoticeHandler
{
    public static async Task<Result<MyTermsView>> HandleAsync(
        DismissTermsNoticeCommand command,
        ITermsRepository terms,
        ITermsQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserIdAsync(command.ActorId, cancellationToken) is not { } userId)
        {
            return Errors.UserNotFound;
        }

        if (await terms.FindVersionForShareAsync(command.VersionId, cancellationToken) is not { } version)
        {
            return TermsVersion.NotFound;
        }

        var now = time.GetUtcNow();
        await terms.DismissAsync(new TermsNoticeDismissal(version.Id, userId, now), cancellationToken);
        return await queries.GetMyTermsAsync(command.ActorId, TermsVersion.DayOf(now), cancellationToken);
    }
}
