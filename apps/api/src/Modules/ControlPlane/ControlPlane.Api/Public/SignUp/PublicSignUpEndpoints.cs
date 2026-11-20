using ControlPlane.Application.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ControlPlane.Api.Public.SignUp;

/// <summary>The product's sign-up, for anyone before sign-in: the sign-in page offers "Create an account" only while it is on (1.24).</summary>
public static class PublicSignUpEndpoints
{
    // A caller without a user is still under the rate limit, by their address (API4), so the read declares its 429. It never answers 401.
    // The answer comes from the configuration, so it reads no database and sends no message.
    public static void Map(RouteGroupBuilder publicRoutes) =>
        publicRoutes.MapGet("/sign-up", GetSignUp).ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static SignUpResponse GetSignUp(SignUpOptions signUp) => new(signUp.Enabled, signUp.TenantLimit);
}
