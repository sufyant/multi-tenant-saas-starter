using ControlPlane.Api.Platform.Plans;

namespace ControlPlane.Api.Public.SignUp;

/// <summary>The trial's plan and how many days it lasts (1.28, 4.29).</summary>
public sealed record SignUpTrialResponse(PlanNameResponse Plan, int Days);
