namespace SharedKernel;

/// <summary>
/// The code of a plan's limit (K5): ControlPlane answers it for its own limits, and a product module for its own (X3), so a screen gives
/// every limit one text. The other codes of plans stay ControlPlane's.
/// </summary>
public static class PlanErrorCodes
{
    public static readonly ErrorCode LimitReached = ErrorCode.Declare("plan.limit_reached");
}
