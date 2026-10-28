namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

/// <summary>
/// The fields the preview of a plan edit sorts by. The first is the default: how far over its furthest key a tenant goes, the furthest
/// first; a descending sort puts the nearest first.
/// </summary>
public enum GetPlanUpdatePreviewSort
{
    Excess,
    Name,
}
