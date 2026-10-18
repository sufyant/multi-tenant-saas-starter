using Audit.Api.Statuses;
using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.GetAuditEntryNames;
using Audit.Application.AuditLogs.ListAuditEntries;
using Audit.Application.Invitations;
using Audit.Application.Members;
using Audit.Application.Tenants;
using ControlPlane.Contracts.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The audit log of the tenant in the route, which its Owners and Admins read. The screen calls it "Activity".</summary>
public static class AuditLogEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/audit-log", ListAuditEntries).RequireAuthorization(AuditLogPermissions.TenantAuditRead);

    internal static AuditEntryResponse ToResponse(AuditEntryView entry) => new(
        entry.Id,
        entry.OccurredAt,
        entry.Operation,
        DetailsOf(entry.Details),
        new AuditActorResponse(ResponseStatus.OfActor(entry.Actor.Kind), entry.Actor.UserId, entry.Actor.DisplayName, entry.Actor.Deleted),
        new AuditTargetResponse(ResponseStatus.OfTarget(entry.Target.Kind), entry.Target.Id, entry.Target.Name, entry.Target.Deleted));

    // Each stored shape answers its response. A shape the response has no type for is a fault, not an answer.
    private static AuditDetailsResponse? DetailsOf(AuditDetails? details) => details switch
    {
        null => null,
        MemberInvitedDetails invited => new MemberInvitedDetailsResponse(RoleOf(invited.Role), invited.ReplacedInvitationId),
        MemberRemovedDetails removed => new MemberRemovedDetailsResponse(RoleOf(removed.Role)),
        MemberRoleChangedDetails changed => new MemberRoleChangedDetailsResponse(RoleOf(changed.OldRole), RoleOf(changed.NewRole)),
        TenantOwnerInvitedDetails invited => new TenantOwnerInvitedDetailsResponse(invited.InvitationId, invited.ReplacedInvitationId),
        TenantRenamedDetails renamed => new TenantRenamedDetailsResponse(renamed.OldName, renamed.NewName),
        TenantSuspendedDetails suspended => new TenantSuspendedDetailsResponse(suspended.Reason),
        _ => throw new ArgumentOutOfRangeException(nameof(details), details.GetType().Name, "The response has no details of this type."),
    };

    // N7k: a role in a response is the one RoleResponse. An entry keeps the role's stored name (K3), which is a built-in role's JSON name;
    // any other name is a custom role, which comes later (A3).
    private static RoleResponse RoleOf(string name) => new(JsonEnum<MemberRole>.Find(name), name);

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

        var entries = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new ListAuditEntriesQuery(page.Value), cancellationToken);
        if (!entries.IsSuccess)
        {
            return entries.Error;
        }

        var named = await bus.InvokeAsync<Result<CursorPage<AuditEntryView>>>(new GetAuditEntryNamesQuery(entries.Value), cancellationToken);
        return named.Map(found => found.Map(ToResponse));
    }
}
