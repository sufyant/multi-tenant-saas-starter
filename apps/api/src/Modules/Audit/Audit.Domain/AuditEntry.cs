using SharedKernel;

namespace Audit.Domain;

/// <summary>
/// One record of an audit log: who did what to what, and when. It holds no personal name and no email address (K3): the actor and a
/// target person are catalog user ids, and the read side resolves their names through ControlPlane. A target that is not a person
/// keeps its name as it was at that moment. Records are business data kept for years, and they are never changed once written.
/// </summary>
internal abstract class AuditRecord
{
    public const int OperationMaxLength = 500;

    public const int TargetNameMaxLength = 100;

    // For EF Core, which reads a record back.
    protected AuditRecord()
    {
        Operation = null!;
    }

    protected AuditRecord(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details)
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

/// <summary>A record of a tenant's own log, under that tenant (row level security).</summary>
internal sealed class AuditEntry : AuditRecord, ITenantEntity
{
    private AuditEntry()
    {
    }

    public AuditEntry(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details = null)
        : base(id, occurredAt, actor, operation, target, details)
    {
    }
}

/// <summary>A record of the platform's log: what system admins did, above tenants.</summary>
internal sealed class PlatformAuditEntry : AuditRecord
{
    private PlatformAuditEntry()
    {
    }

    public PlatformAuditEntry(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details = null)
        : base(id, occurredAt, actor, operation, target, details)
    {
    }
}

/// <summary>Who acted. A user is named by their catalog user id; the platform is named by its kind.</summary>
internal sealed record AuditActor(AuditActorKind Kind, Guid? UserId)
{
    public static readonly AuditActor Platform = new(AuditActorKind.Platform, null);


    public static AuditActor User(Guid userId) => new(AuditActorKind.User, userId);
}

/// <summary>What the action was done to. A person carries no name (K3).</summary>
internal sealed record AuditTarget(AuditTargetKind Kind, Guid Id, string? Name)
{
    public static AuditTarget Tenant(Guid tenantId, string? name) => new(AuditTargetKind.Tenant, tenantId, name);

    public static AuditTarget User(Guid userId) => new(AuditTargetKind.User, userId, null);

    /// <summary>An invitation, by its id only: its email address is a person's (K3).</summary>
    public static AuditTarget Invitation(Guid invitationId) => new(AuditTargetKind.Invitation, invitationId, null);
}

internal enum AuditActorKind
{
    User,
    Platform,
}

internal enum AuditTargetKind
{
    Tenant,
    User,
    Invitation,
}

internal enum AuditEntryKind
{
    Command,
}
