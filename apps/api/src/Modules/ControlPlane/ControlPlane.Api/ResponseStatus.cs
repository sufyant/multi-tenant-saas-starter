using ControlPlane.Api.Admin;
using ControlPlane.Api.Me;

namespace ControlPlane.Api;

/// <summary>
/// Maps each of ControlPlane's statuses to the status enum of a response (N7). Every response maps its status here when it is built,
/// so a saved idempotent result, which holds ControlPlane's name, replays to the same answer. A status a response cannot carry is a
/// fault, not an answer.
/// </summary>
internal static class ResponseStatus
{
    public static TenantResponseStatus OfTenant(string status) => status switch
    {
        "Provisioning" => TenantResponseStatus.Provisioning,
        "Active" => TenantResponseStatus.Active,
        "Suspended" => TenantResponseStatus.Suspended,
        "Cancelled" => TenantResponseStatus.Cancelled,
        _ => throw Unknown(status),
    };

    // A member sees only an active or a suspended tenant (T4), and an invitation is accepted only in an active one.
    public static MyTenantResponseStatus OfMyTenant(string status) => status switch
    {
        "Active" => MyTenantResponseStatus.Active,
        "Suspended" => MyTenantResponseStatus.Suspended,
        _ => throw Unknown(status),
    };

    public static UserResponseStatus OfUser(string status) => status switch
    {
        "Active" => UserResponseStatus.Active,
        "Suspended" => UserResponseStatus.Suspended,
        "Deleted" => UserResponseStatus.Deleted,
        _ => throw Unknown(status),
    };

    private static ArgumentOutOfRangeException Unknown(string status) =>
        new(nameof(status), status, "The response has no value for this status of ControlPlane.");
}
