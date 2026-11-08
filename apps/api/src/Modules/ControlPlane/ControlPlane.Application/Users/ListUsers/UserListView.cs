using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

/// <summary>A page of the platform's user list, and how many users of the whole list are in each status.</summary>
public sealed record UserListView(ListPage<UserView> Page, UserStatusCountsView StatusCounts);
