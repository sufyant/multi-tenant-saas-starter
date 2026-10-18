using System.Reflection;
using Audit.Api;
using ControlPlane.Api;
using ControlPlane.Api.Platform.Tenants;
using ControlPlane.Api.Platform.Users;

namespace Api.ArchitectureTests;

// A14 and B9: a Domain status crosses to a module's Api as its name, and the Api maps the name to the enum of a response (N7). A new
// Domain value compiles and then answers 500, so this test feeds every name of each Domain enum through each map. The Domain enums and
// the ResponseStatus of each module's Api are internal to their modules, so this system-wide test reads them by reflection, as RoleNameTests does.
public class DomainNameMapTests
{
    private static readonly Type ResponseStatus = typeof(ControlPlaneModule).Assembly.GetType("ControlPlane.Api.Statuses.ResponseStatus", throwOnError: true)!;

    private static readonly Type AuditResponseStatus = typeof(AuditModule).Assembly.GetType("Audit.Api.Statuses.ResponseStatus", throwOnError: true)!;

    private static readonly string[] TenantStatusNames = NamesOf("ControlPlane.Domain", "ControlPlane.Domain.Tenants.TenantStatus");

    public static TheoryData<string> TenantStatuses => [.. TenantStatusNames];

    // A pending invitation and an Owner invitation both answer the one invitation state (N7).
    public static TheoryData<string> InvitationStates =>
    [
        .. NamesOf("ControlPlane.Domain", "ControlPlane.Domain.Tenants.InvitationState"),
        .. NamesOf("ControlPlane.Domain", "ControlPlane.Domain.Tenants.OwnerInvitationState"),
    ];

    public static TheoryData<string> UserStatuses => [.. NamesOf("ControlPlane.Domain", "ControlPlane.Domain.Users.UserStatus")];

    public static TheoryData<TenantResponseStatus> TenantFilters => [.. Enum.GetValues<TenantResponseStatus>()];

    public static TheoryData<UserResponseStatus> UserFilters => [.. Enum.GetValues<UserResponseStatus>()];

    public static TheoryData<string> AuditActorKinds => [.. NamesOf("Audit.Domain", "Audit.Domain.AuditEntries.AuditActorKind")];

    public static TheoryData<string> AuditTargetKinds => [.. NamesOf("Audit.Domain", "Audit.Domain.AuditEntries.AuditTargetKind")];

    [Theory]
    [MemberData(nameof(TenantStatuses))]
    public void MapATenantStatus_EveryDomainStatus_HasAResponseStatus(string status)
    {
        Map("OfTenant", status).ShouldNotBeNull();
    }

    // A member sees only an active, a suspended or a closed tenant (T4). A new Domain status fails here until each member map decides it.
    [Fact]
    public void MapATenantStatus_TheStatusesAMemberNeverSees_AreProvisioningAndCancelled()
    {
        TenantStatusNames.Except(["Active", "Suspended", "Closed"]).ShouldBe(["Provisioning", "Cancelled"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Closed")]
    public void MapATenantStatus_EveryStatusAMemberSees_HasAMemberResponseStatus(string status)
    {
        Map("OfMyTenant", status).ShouldNotBeNull();
        Map("OfUserMembership", status).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(InvitationStates))]
    public void MapAnInvitationState_EveryDomainState_HasAResponseState(string state)
    {
        Map("OfInvitation", state).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(UserStatuses))]
    public void MapAUserStatus_EveryDomainStatus_HasAResponseStatus(string status)
    {
        Map("OfUser", status).ShouldNotBeNull();
    }

    // A list filter names a response status; the query reads the Domain status of that name.
    [Theory]
    [MemberData(nameof(TenantFilters))]
    public void FilterTenants_EveryResponseStatus_NamesADomainStatus(TenantResponseStatus status)
    {
        TenantStatusNames.ShouldContain((string)ResponseStatus.GetMethod("OfTenant", [typeof(TenantResponseStatus)])!.Invoke(null, [status])!);
    }

    [Theory]
    [MemberData(nameof(UserFilters))]
    public void FilterUsers_EveryResponseStatus_NamesADomainStatus(UserResponseStatus status)
    {
        NamesOf("ControlPlane.Domain", "ControlPlane.Domain.Users.UserStatus")
            .ShouldContain((string)ResponseStatus.GetMethod("OfUser", [typeof(UserResponseStatus)])!.Invoke(null, [status])!);
    }

    // Audit's kinds keep the Domain's names, and its Api maps each name to the response's kind.
    [Theory]
    [MemberData(nameof(AuditActorKinds))]
    public void MapAnAuditActor_EveryDomainKind_HasAResponseKind(string kind)
    {
        AuditMap("OfActor", kind).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(AuditTargetKinds))]
    public void MapAnAuditTarget_EveryDomainKind_HasAResponseKind(string kind)
    {
        AuditMap("OfTarget", kind).ShouldNotBeNull();
    }

    private static object? Map(string method, string name) => ResponseStatus.GetMethod(method, [typeof(string)])!.Invoke(null, [name]);

    private static object? AuditMap(string method, string name) => AuditResponseStatus.GetMethod(method, [typeof(string)])!.Invoke(null, [name]);

    private static string[] NamesOf(string assembly, string type) => Enum.GetNames(Solution.Load(assembly).GetType(type, throwOnError: true)!);
}
