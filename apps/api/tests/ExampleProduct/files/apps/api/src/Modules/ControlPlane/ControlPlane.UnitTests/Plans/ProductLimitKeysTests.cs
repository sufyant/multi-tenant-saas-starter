using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

// The example product "Projects" declares a limit that depends on its feature (X1). These tests run only in the job "Example product:
// build and test" (X4).
public sealed class ProductLimitKeysTests
{
    [Fact]
    public void GenerateEntitlements_TheProductsLimit_IsOnTheLimitList() => Limits.ProjectsMax.ShouldBe("projects.max");

    [Fact]
    public void ListKeys_TheProductsLimit_IsALimitOfItsFeature() =>
        (EntitlementKeys.KindOf(ProductLimitKeys.ProjectsMax), EntitlementKeys.FeatureOf(ProductLimitKeys.ProjectsMax))
            .ShouldBe(((EntitlementKind?)EntitlementKind.Limit, (string?)ProductFeatureKeys.ProjectsAccess));
}
