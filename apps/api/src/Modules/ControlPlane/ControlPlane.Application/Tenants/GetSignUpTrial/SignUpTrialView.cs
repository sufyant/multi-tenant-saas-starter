using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetSignUpTrial;

/// <summary>
/// The trial's plan and its length in whole days. No plan when the plan of the configuration does not exist, as a new tenant then starts no
/// trial.
/// </summary>
public sealed record SignUpTrialView(PlanNames? Plan, int Days);
