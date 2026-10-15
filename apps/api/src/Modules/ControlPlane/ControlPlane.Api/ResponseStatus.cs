namespace ControlPlane.Api;

/// <summary>
/// The status values the API answers with: one lowercase word for each of ControlPlane's statuses. Every response maps its status
/// here when it is built, so a saved idempotent result, which holds ControlPlane's name, replays to the same answer.
/// </summary>
internal static class ResponseStatus
{
    // ControlPlane's name of each status and the value the API answers with. The OpenAPI document reads its values from here (N7).
    private static readonly (string Name, string Value)[] Statuses =
    [
        ("Provisioning", "provisioning"),
        ("Active", "active"),
        ("Suspended", "suspended"),
        ("Cancelled", "cancelled"),
        ("Deleted", "deleted"),
    ];

    public static IReadOnlyList<string> Values { get; } = [.. Statuses.Select(status => status.Value)];

    public static string Of(string status) =>
        Array.Find(Statuses, known => known.Name == status) is { Value: { } value }
            ? value
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Every status of ControlPlane has a response value.");
}
