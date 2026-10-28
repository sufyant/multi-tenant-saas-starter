namespace ControlPlane.Api.Platform.Plans;

/// <summary>A plan as another resource names it: its id and its name.</summary>
public sealed record PlanNameResponse(Guid Id, string Name);
