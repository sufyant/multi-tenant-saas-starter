namespace Audit.Domain.AuditEntries;

/// <summary>Who acted. A user is named by their user id; the platform is named by its kind.</summary>
internal sealed record AuditActor(AuditActorKind Kind, Guid? UserId)
{
    public static readonly AuditActor Platform = new(AuditActorKind.Platform, null);

    public static AuditActor User(Guid userId) => new(AuditActorKind.User, userId);
}
