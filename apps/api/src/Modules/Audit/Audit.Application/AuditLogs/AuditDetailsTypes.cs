using System.Text.Json;
using Audit.Application.Invitations;
using Audit.Application.Members;
using Audit.Application.Tenants;
using Audit.Domain;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The decision about the details of every operation of <see cref="AuditOperations"/>: the one type of its details, or none (K3).
/// <c>AuditOperationTests</c> fails for an operation without a decision.
/// </summary>
public static class AuditDetailsTypes
{
    // The operations with details, from AuditOperations, for the discriminator of Audit's Api project, which does not reference Audit's
    // Domain (section 2).
    public const string MemberInvited = AuditOperations.MemberInvited;

    public const string MemberRemoved = AuditOperations.MemberRemoved;

    public const string MemberRoleChanged = AuditOperations.MemberRoleChanged;

    public const string TenantOwnerInvited = AuditOperations.TenantOwnerInvited;

    public const string TenantRenamed = AuditOperations.TenantRenamed;

    public const string TenantSuspended = AuditOperations.TenantSuspended;

    /// <summary>Every operation, with the type of its details, or null for an operation without details.</summary>
    public static IReadOnlyDictionary<string, Type?> OfOperation { get; } = new Dictionary<string, Type?>(StringComparer.Ordinal)
    {
        [AuditOperations.InvitationRevoked] = null,
        [AuditOperations.MemberInvited] = typeof(MemberInvitedDetails),
        [AuditOperations.MemberJoined] = null,
        [AuditOperations.MemberRemoved] = typeof(MemberRemovedDetails),
        [AuditOperations.MemberRoleChanged] = typeof(MemberRoleChangedDetails),
        [AuditOperations.TenantClosed] = null,
        [AuditOperations.TenantCreated] = null,
        [AuditOperations.TenantOwnerInvited] = typeof(OwnerInvitedDetails),
        [AuditOperations.TenantReactivated] = null,
        [AuditOperations.TenantRenamed] = typeof(TenantRenamedDetails),
        [AuditOperations.TenantReopened] = null,
        [AuditOperations.TenantSuspended] = typeof(TenantSuspendedDetails),
        [AuditOperations.UserDeleted] = null,
        [AuditOperations.UserPreferencesChanged] = null,
        [AuditOperations.UserReactivated] = null,
        [AuditOperations.UserRenamed] = null,
        [AuditOperations.UserSuspended] = null,
    };

    /// <summary>
    /// The stored details of an entry as the type of its operation, or null when the entry has none. A stored object that its operation
    /// has no type for is a fault, not an answer.
    /// </summary>
    public static AuditDetails? Read(string operation, string? details) =>
        details is null ? null
        : OfOperation[operation] is { } type ? (AuditDetails)JsonSerializer.Deserialize(details, type, JsonSerializerOptions.Web)!
        : throw new InvalidOperationException($"The operation {operation} keeps no details.");
}
