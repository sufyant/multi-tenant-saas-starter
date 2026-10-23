using System.Reflection;

namespace Api.ArchitectureTests;

// D1, part 3: Audit keeps the name of a tenant as it was (K3), in a column of its own width. Audit.Domain sees only SharedKernel (section 2), so it
// cannot read ControlPlane's limit, and the two modules keep a constant each. A longer tenant name would fail the audit insert and send
// the message to the dead letter queue, so this test keeps the audit column at least as wide. Both constants are internal to their
// modules, so the test reads them by reflection.
public sealed class AuditTargetNameTests
{
    [Fact]
    public void RecordATargetName_TheLongestTenantName_FitsTheAuditColumn()
    {
        var tenantName = ConstantOf("ControlPlane.Domain", "ControlPlane.Domain.Tenants.Tenant", "NameMaxLength");
        var targetName = ConstantOf("Audit.Domain", "Audit.Domain.AuditEntries.AuditEntry", "TargetNameMaxLength");

        targetName.ShouldBeGreaterThanOrEqualTo(tenantName);
    }

    private static int ConstantOf(string assembly, string type, string name) =>
        (int)Solution.Load(assembly).GetType(type, throwOnError: true)!.GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
}
