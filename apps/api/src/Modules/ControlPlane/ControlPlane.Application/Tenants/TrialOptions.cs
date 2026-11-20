namespace ControlPlane.Application.Tenants;

/// <summary>
/// The trial of a user's own tenant (section <c>ControlPlane:Trial</c>, step 40d), checked on start; public because a handler takes it
/// (W9). A trial exists only while billing is on (the maintainer's decision of 2026-11-20).
/// </summary>
public sealed class TrialOptions
{
    public const string Section = "ControlPlane:Trial";

    /// <summary>
    /// The plan of the trial. The default is the seeded Business plan (1.28: "Your 14-day Business trial"). A tenant starts no trial when
    /// the plan does not exist.
    /// </summary>
    public Guid PlanId { get; set; } = new("0199c000-0000-7000-8000-000000000940");

    /// <summary>How many days the trial lasts. Positive.</summary>
    public int Days { get; set; } = 14;
}
