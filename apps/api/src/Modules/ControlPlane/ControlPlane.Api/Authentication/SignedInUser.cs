using System.Security.Claims;

namespace ControlPlane.Api.Authentication;

internal static class SignedInUser
{
    // Every endpoint of the module requires a signed-in user, so the identity provider's user id is always there.
    public static string Id(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The request has no signed-in user.");
}
