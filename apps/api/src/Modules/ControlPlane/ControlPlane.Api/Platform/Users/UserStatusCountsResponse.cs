namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// How many users of the list are in each status, with the search and every filter but the status: the tabs of the Users list.
/// </summary>
public sealed record UserStatusCountsResponse(int Active, int Suspended, int Deleted);
