using ControlPlane.Api.Platform;
using ControlPlane.Api.Me;

namespace ControlPlane.Api;

/// <summary>
/// Maps each of ControlPlane's statuses to the status enum of a response (N7), and the status a list filter names back to ControlPlane's
/// (K5). Every response maps its status here when it is built, so a saved idempotent result, which holds ControlPlane's name, replays to
/// the same answer. A status a response cannot carry is a fault, not an answer.
/// </summary>
internal static class ResponseStatus
{
    public static TenantResponseStatus OfTenant(string status) => status switch
    {
        "Provisioning" => TenantResponseStatus.Provisioning,
        "Active" => TenantResponseStatus.Active,
        "Suspended" => TenantResponseStatus.Suspended,
        "Cancelled" => TenantResponseStatus.Cancelled,
        "Closed" => TenantResponseStatus.Closed,
        _ => throw Unknown(status),
    };

    // A member sees only an active, a suspended or a closed tenant (T4), and an invitation is accepted only in an active one.
    public static MyTenantResponseStatus OfMyTenant(string status) => status switch
    {
        "Active" => MyTenantResponseStatus.Active,
        "Suspended" => MyTenantResponseStatus.Suspended,
        "Closed" => MyTenantResponseStatus.Closed,
        _ => throw Unknown(status),
    };

    public static UserResponseStatus OfUser(string status) => status switch
    {
        "Active" => UserResponseStatus.Active,
        "Suspended" => UserResponseStatus.Suspended,
        "Deleted" => UserResponseStatus.Deleted,
        _ => throw Unknown(status),
    };

    // A membership exists only in a tenant past its creation's pivot (section 6).
    public static UserMembershipResponseTenantStatus OfUserMembership(string status) => status switch
    {
        "Active" => UserMembershipResponseTenantStatus.Active,
        "Suspended" => UserMembershipResponseTenantStatus.Suspended,
        "Closed" => UserMembershipResponseTenantStatus.Closed,
        _ => throw Unknown(status),
    };

    public static InvitationPreviewResponseState OfInvitationPreview(string state) => state switch
    {
        "Pending" => InvitationPreviewResponseState.Pending,
        "Expired" => InvitationPreviewResponseState.Expired,
        "Accepted" => InvitationPreviewResponseState.Accepted,
        "Cancelled" => InvitationPreviewResponseState.Cancelled,
        _ => throw Unknown(state),
    };

    public static InviterNameResponseKind OfInviter(string kind) => kind switch
    {
        "User" => InviterNameResponseKind.User,
        "Platform" => InviterNameResponseKind.Platform,
        _ => throw Unknown(kind),
    };

    public static OwnerInvitationResponseState OfOwnerInvitation(string state) => state switch
    {
        "Pending" => OwnerInvitationResponseState.Pending,
        "Expired" => OwnerInvitationResponseState.Expired,
        "Cancelled" => OwnerInvitationResponseState.Cancelled,
        _ => throw Unknown(state),
    };

    // A filter's value is the status of a response; the query names ControlPlane's status.
    public static string OfTenant(TenantResponseStatus status) => status switch
    {
        TenantResponseStatus.Provisioning => "Provisioning",
        TenantResponseStatus.Active => "Active",
        TenantResponseStatus.Suspended => "Suspended",
        TenantResponseStatus.Cancelled => "Cancelled",
        TenantResponseStatus.Closed => "Closed",
        _ => throw Unknown(status.ToString()),
    };

    public static string OfUser(UserResponseStatus status) => status switch
    {
        UserResponseStatus.Active => "Active",
        UserResponseStatus.Suspended => "Suspended",
        UserResponseStatus.Deleted => "Deleted",
        _ => throw Unknown(status.ToString()),
    };

    private static ArgumentOutOfRangeException Unknown(string status) =>
        new(nameof(status), status, "The response has no value for this status of ControlPlane.");
}
