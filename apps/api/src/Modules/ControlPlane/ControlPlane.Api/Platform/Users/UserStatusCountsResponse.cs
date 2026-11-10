namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// How many users are in each status, without the search or any filter: the tabs of the Users list.
/// </summary>
public sealed record UserStatusCountsResponse(int Active, int Suspended, int Deleted);
