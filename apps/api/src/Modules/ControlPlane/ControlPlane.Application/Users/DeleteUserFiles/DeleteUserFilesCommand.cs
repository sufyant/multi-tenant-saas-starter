using SharedKernel;

namespace ControlPlane.Application.Users.DeleteUserFiles;

/// <summary>Deletes every file of a deleted user, their prefix users/{userId}/, after the commit of the user's erasure (A8).</summary>
[MessageName("delete-user-files")]
public sealed record DeleteUserFilesCommand(Guid UserId);
