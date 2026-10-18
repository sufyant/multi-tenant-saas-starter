namespace ControlPlane.Domain.SystemAdmins;

/// <summary>
/// A grant that makes a user a system admin: a person who works for the provider. It is an extra grant on the single identity,
/// recorded with when it was granted. The first one comes from configuration (section 6).
/// </summary>
internal sealed class SystemAdmin(Guid userId, DateTimeOffset grantedAt)
{
    public Guid UserId { get; private init; } = userId;

    public DateTimeOffset GrantedAt { get; private init; } = grantedAt;
}
