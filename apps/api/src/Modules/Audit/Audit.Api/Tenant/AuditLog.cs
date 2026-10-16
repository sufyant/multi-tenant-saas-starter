using System.Text.Json;
using System.Text.Json.Serialization;
using Audit.Application.AuditLogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Tenant;

/// <summary>The audit log of the tenant in the route, which its Owners and Admins read. The screen calls it "Activity".</summary>
public static class AuditLog
{
    // The permission catalogue is in ControlPlane.Contracts, which this project does not reference (section 2). The name is stated
    // here as well; EndpointAccessTests fails for an endpoint whose permission the catalogue does not hold.
    private const string TenantAuditRead = "tenant.audit.read";

    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/audit-log", ListAuditEntries).RequireAuthorization(TenantAuditRead);

    // The tenant comes from the route: the host has declared it, and row level security keeps the log to it. K5's exception: a cursor,
    // newest first, and no total.
    private static async Task<Result<CursorPage<AuditEntryResponse>>> ListAuditEntries(
        string? cursor,
        int? limit,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var page = CursorRequest.Create(cursor, limit);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        var entries = await bus.InvokeAsync<CursorPage<AuditEntrySummary>>(new ListAuditEntries(page.Value), cancellationToken);
        return AuditEntryResponses.Of(entries);
    }
}

/// <summary>
/// One record of an audit log: when, the operation (<c>member.invited</c>), who acted, what it was done to, and the details the record
/// keeps (a JSON object, or null). Names are resolved when the log is read: a deleted user reads "Deleted user". It holds no email
/// address.
/// </summary>
public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Operation,
    JsonElement? Details,
    AuditActorResponse Actor,
    AuditTargetResponse? Target);

/// <summary>
/// Who acted: a user, by catalog user id and name; the platform, that is a system admin seen from a tenant's log; or the identity
/// provider. The name is null for a user the catalog does not know.
/// </summary>
public sealed record AuditActorResponse(AuditActorResponseKind Kind, Guid? UserId, string? DisplayName);

/// <summary>
/// What the action was done to: a tenant with its name at that moment, a user with their name now, or an invitation, which has no
/// name. A record written before the audit kept targets has none.
/// </summary>
public sealed record AuditTargetResponse(AuditTargetResponseKind Kind, Guid Id, string? Name);

/// <summary>The kind of actor of an audit record.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditActorResponseKind>))]
public enum AuditActorResponseKind
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("platform")]
    Platform,

    [JsonStringEnumMemberName("identityProvider")]
    IdentityProvider,
}

/// <summary>The kind of target of an audit record.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditTargetResponseKind>))]
public enum AuditTargetResponseKind
{
    [JsonStringEnumMemberName("tenant")]
    Tenant,

    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("invitation")]
    Invitation,
}

// The kinds keep the Domain's names (User, Tenant), so each maps to the response's enum by name.
internal static class AuditEntryResponses
{
    public static CursorPage<AuditEntryResponse> Of(CursorPage<AuditEntrySummary> page) => page.Map(entry => new AuditEntryResponse(
        entry.Id,
        entry.OccurredAt,
        entry.Operation,
        entry.Details is { } details ? JsonElement.Parse(details) : null,
        new AuditActorResponse(Enum.Parse<AuditActorResponseKind>(entry.Actor.Kind), entry.Actor.UserId, entry.Actor.DisplayName),
        entry.Target is { } target ? new AuditTargetResponse(Enum.Parse<AuditTargetResponseKind>(target.Kind), target.Id, target.Name) : null));
}
