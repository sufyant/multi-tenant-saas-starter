using ControlPlane.Contracts;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;

namespace ControlPlane.UnitTests;

// The example product "Projects" fills the product's lists (X1). These tests run only in the job "Example product: build and test",
// which copies tests/ExampleProduct/files over the template (X4).
public sealed class ProductPermissionsTests
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
}
