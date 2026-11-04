using SharedKernel;

namespace ControlPlane.Application.Users.DeleteUserFiles;

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). A failure retries (W11); once the retries
// are spent, the move to the dead letter queue logs an error, which is the alarm (section 7, "Error tracking", rule 4), and the deletion is
// not done until the maintainer retries it or a second deletion of the user asks again (GDPR, Article 17). An empty prefix is done.
public static class DeleteUserFilesHandler
{
    public static async Task HandleAsync(DeleteUserFilesCommand command, IFileStore files, CancellationToken cancellationToken)
    {
        foreach (var prefix in FileKeys.AllOfUser(command.UserId))
        {
            await files.DeletePrefixAsync(prefix, cancellationToken);
        }
    }
}
