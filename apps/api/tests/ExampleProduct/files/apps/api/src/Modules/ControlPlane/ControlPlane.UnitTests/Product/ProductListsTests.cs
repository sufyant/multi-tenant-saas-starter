using ControlPlane.Contracts;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Roles;

namespace ControlPlane.UnitTests.Product;

// The example product "Projects" fills the product's lists (X1); these tests run only in the job "Example product: build and test",
// which copies tests/ExampleProduct/files over the template (X4).
public sealed class ProductListsTests
{
    [Fact]
    public void GeneratePermissions_TheProductsPermissions_AreOnThePermissionList() =>
        (Permissions.TenantProjectsRead, Permissions.TenantProjectsCreate, Permissions.TenantProjectsDelete, Permissions.PlatformProjectsRead)
            .ShouldBe(("tenant.projects.read", "tenant.projects.create", "tenant.projects.delete", "platform.projects.read"));

    [Fact]
    public void ListPools_TheProductsPermissions_AreInTheirPools() =>
        (PermissionPools.TenantPool.IsSupersetOf(ProductPermissions.Tenant), PermissionPools.PlatformPool.Contains(ProductPermissions.PlatformProjectsRead))
            .ShouldBe((true, true));

    [Theory]
    [InlineData("tenant.projects.read", new[] { "Owner", "Admin", "Member" })]
    [InlineData("tenant.projects.create", new[] { "Owner", "Admin", "Member" })]
    [InlineData("tenant.projects.delete", new[] { "Owner", "Admin" })]
    public void GrantBuiltInRoles_AProductPermission_GoesToTheRolesTheProductNames(string permission, string[] holders) =>
        BuiltInRoles.Holding(permission).Select(role => role.ToString()).ShouldBe(holders);

    [Fact]
    public void GenerateEntitlements_TheProductsKeys_AreOnTheEntitlementList() =>
        (Features.Projects, Limits.ProjectsMax).ShouldBe(("projects", "projects.max"));

    [Fact]
    public void ListKeys_TheProductsKeys_HaveTheirKindsAndTheirFeature() =>
        (EntitlementKeys.KindOf("projects"), EntitlementKeys.KindOf("projects.max"), EntitlementKeys.FeatureOf("projects.max"))
            .ShouldBe(((EntitlementKind?)EntitlementKind.Feature, (EntitlementKind?)EntitlementKind.Limit, (string?)"projects"));
}
