namespace ControlPlane.Application.Plans;

/// <summary>A plan as a command names it: its id and its name of that moment.</summary>
public sealed record PlanNames(Guid Id, string Name);
