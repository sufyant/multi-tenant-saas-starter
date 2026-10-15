namespace ControlPlane.Api;

/// <summary>
/// The status values the API answers with: one lowercase word for each of ControlPlane's statuses. Every response maps its status
/// here when it is built, so a saved idempotent result, which holds ControlPlane's name, replays to the same answer.
/// </summary>
internal static class ResponseStatus
{
    public static string Of(string status) => status switch
    {
        "Provisioning" => "provisioning",
        "Active" => "active",
        "Suspended" => "suspended",
        "Cancelled" => "cancelled",
        "Deleted" => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Every status of ControlPlane has a response value."),
    };
}
