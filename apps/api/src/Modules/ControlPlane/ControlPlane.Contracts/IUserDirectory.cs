using SharedKernel;

namespace ControlPlane.Contracts;

/// <summary>The catalog lookup the host needs on every me and tenant route, before anything else (A9).</summary>
public interface IUserDirectory
{
    /// <summary>
    /// Whether the user may act for themselves or in a tenant. A suspended user is refused with a reason. A user the catalog does
    /// not know yet, such as one about to accept their first invitation, may.
    /// </summary>
    Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken);
}
