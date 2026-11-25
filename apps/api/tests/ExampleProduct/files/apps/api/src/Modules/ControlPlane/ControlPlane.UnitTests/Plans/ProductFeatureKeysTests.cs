using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

// The example product "Projects" declares a feature (X1). These tests run only in the job "Example product: build and test" (X4).
public sealed class ProductFeatureKeysTests
{
    [Fact]
    public void GenerateEntitlements_TheProductsFeature_IsOnTheFeatureList() => Features.ProjectsAccess.ShouldBe("projects.access");

    [Fact]
    public void ListKeys_TheProductsFeature_IsAFeature() => EntitlementKeys.KindOf(ProductFeatureKeys.ProjectsAccess).ShouldBe(EntitlementKind.Feature);
}
