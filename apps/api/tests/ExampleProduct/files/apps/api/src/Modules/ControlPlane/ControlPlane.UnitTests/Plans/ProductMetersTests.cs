using ControlPlane.Application.Plans;

namespace ControlPlane.UnitTests.Plans;

// The example product "Projects" defines a meter (X5). This test runs only in the job "Example product: build and test" (X4).
public sealed class ProductMetersTests
{
    [Fact]
    public void ListMeters_TheProductsMeter_IsAMeterOfTheCode() =>
        MeterCode.Code.Meters.ShouldContain(new MeterView("projects.exports", "Exports of a project that the members start."));
}
