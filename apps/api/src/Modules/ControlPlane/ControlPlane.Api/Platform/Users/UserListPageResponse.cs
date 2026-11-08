namespace ControlPlane.Api.Platform.Users;

/// <summary>A page of the user list (K5), and how many users of the whole list are in each status.</summary>
public sealed record UserListPageResponse(IReadOnlyList<UserResponse> Items, int Page, int PageSize, int Total, UserStatusCountsResponse StatusCounts);
