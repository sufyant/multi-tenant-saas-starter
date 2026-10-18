using System.Reflection;
using ControlPlane.Domain;

namespace ControlPlane.UnitTests;

public class PermissionPoolTests
{
    // A tenant's custom roles choose from the tenant pool, so a system permission must never be part of it.
    [Fact]
    public void ComparePools_TenantAndSystem_ShareNoPermission()
    {
        var shared = PermissionPools.TenantPool.Intersect(PermissionPools.SystemPool);

        shared.ShouldBeEmpty();
    }

    [Fact]
    public void NameSystemPermissions_EveryOne_StartsWithAdmin()
    {
        var misnamed = PermissionPools.SystemPool.Where(permission => !permission.StartsWith("platform.", StringComparison.Ordinal));

        misnamed.ShouldBeEmpty();
    }

    // B18: PermissionPools is the one source of the permission names. The catalogue in Contracts is generated from its constants, and CI
    // fails when the generated file differs, so this test checks only that each constant is in a pool.
    [Fact]
    public void DeclarePermissions_EveryName_IsInExactlyTheDomainPools()
    {
        var names = typeof(PermissionPools).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        names.ShouldBe(PermissionPools.TenantPool.Concat(PermissionPools.SystemPool), ignoreOrder: true);
    }
}
