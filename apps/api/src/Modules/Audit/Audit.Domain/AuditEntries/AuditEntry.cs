namespace Audit.Domain.AuditEntries;

/// <summary>
/// One entry of an audit log: who did what to what, and when. It holds no personal name and no email address (K3): the actor and a
/// target person are catalog user ids, and the read side resolves their names through ControlPlane. A target that is not a person
/// keeps its name as it was at that moment. Entries are business data kept for years, and they are never changed once written.
/// </summary>
internal abstract class AuditEntry
{
    public const int OperationMaxLength = 500;

    public const int TargetNameMaxLength = 100;

    // For EF Core, which reads an entry back.
    protected AuditEntry()
    {
        Operation = null!;
    }

    protected AuditEntry(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details)
    {
        (Id, OccurredAt, Operation, Details) = (id, occurredAt, operation, details);
        (ActorKind, ActorId) = (actor.Kind, actor.UserId);
        (TargetKind, TargetId, TargetName) = (target.Kind, target.Id, target.Name);
    }

    public Guid Id { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>Who acted.</summary>
    public AuditActorKind ActorKind { get; private init; }

    /// <summary>The catalog user id of an actor that is a user; none for the platform.</summary>
    public Guid? ActorId { get; private init; }

    public AuditEntryKind Kind { get; private init; } = AuditEntryKind.Command;

    public string Operation { get; private init; }

    public AuditTargetKind TargetKind { get; private init; }

    public Guid TargetId { get; private init; }

    /// <summary>The target's name at that moment, only when the target is not a person: a tenant's name.</summary>
    public string? TargetName { get; private init; }

    public string? Details { get; private init; }
}
