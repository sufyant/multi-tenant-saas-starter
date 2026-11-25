using SharedKernel;

namespace ControlPlane.Application.Plans.GetMeters;

/// <summary>Every meter the code defines, for the plan editor, which offers a meter that the plan does not set yet (X5).</summary>
[MessageName("get-meters")]
public sealed record GetMetersQuery;
