using ControlPlane.Api.Platform.Plans;
using ControlPlane.Application.Tenants.GetBillingEnabled;
using ControlPlane.Application.Tenants.GetSignUpTrial;
using ControlPlane.Application.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace ControlPlane.Api.Public.SignUp;

/// <summary>The product's sign-up, for anyone before sign-in: the sign-in page offers "Create an account" only while it is on (1.24).</summary>
public static class PublicSignUpEndpoints
{
    // A caller without a user is still under the rate limit, by their address (API4), so the read declares its 429. It never answers 401.
    // The answer comes from the configuration, with the name of the trial's plan; it sends no message. A trial exists only while billing is
    // on (the maintainer's decision of 2026-11-20).
    public static void Map(RouteGroupBuilder publicRoutes) =>
        publicRoutes.MapGet("/sign-up", GetSignUp).ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<SignUpResponse> GetSignUp(SignUpOptions signUp, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!await bus.InvokeAsync<bool>(new GetBillingEnabledQuery(), cancellationToken))
        {
            return new(signUp.Enabled, signUp.TenantLimit, null);
        }

        var trial = await bus.InvokeAsync<SignUpTrialView>(new GetSignUpTrialQuery(), cancellationToken);
        return new(
            signUp.Enabled,
            signUp.TenantLimit,
            trial.Plan is { } plan ? new SignUpTrialResponse(new PlanNameResponse(plan.Id, plan.Name), trial.Days) : null);
    }
}
