namespace ControlPlane.Application.Plans.GetMeters;

public static class GetMetersHandler
{
    public static IReadOnlyList<MeterView> Handle(GetMetersQuery query, MeterCode meters) => meters.Meters;
}
