using ControlPlane.Contracts.Users;

namespace Billing.Application.BillingEmails.GetMember;

// The tenant door has proved the member, so they are a user.
public static class GetMemberHandler
{
    public static async Task<Guid> HandleAsync(GetMemberQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        (await users.FindContactAsync(query.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("The tenant door proved the member, so they are a user.")).UserId;
}
