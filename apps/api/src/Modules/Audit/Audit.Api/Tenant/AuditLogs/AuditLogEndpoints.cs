using Audit.Api.Statuses;
using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.GetAuditEntryNames;
using Audit.Application.AuditLogs.ListAuditEntries;
using Audit.Application.Invitations;
using Audit.Application.Members;
using Audit.Application.Tenants;
using Audit.Application.Roles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The audit log of the tenant in the route, which its Owners and Admins read. The screen calls it "Activity".</summary>
public static class AuditLogEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/audit-log", ListAuditEntries).RequireAuthorization(AuditLogPermissions.TenantAuditRead).WithMetadata(new NotPlanGated());

    internal static AuditEntryResponse ToResponse(AuditEntryView entry) => new(
        entry.Id,
        entry.OccurredAt,
        entry.Operation,
        DetailsOf(entry),
        new AuditActorResponse(ResponseStatus.OfActor(entry.Actor.Kind), entry.Actor.UserId, entry.Actor.DisplayName, entry.Actor.Deleted),
        new AuditTargetResponse(ResponseStatus.OfTarget(entry.Target.Kind), entry.Target.Id, entry.Target.Name, entry.Target.Deleted));

    // Each stored shape answers its response. A shape the response has no type for is a fault, not an answer. The user the details point
    // to is named on read; a page that was never named answers no name, as for an id no user has.
    private static AuditDetailsResponse? DetailsOf(AuditEntryView entry) => entry.Details switch
    {
        null => null,
        MemberInvitedDetails invited => new MemberInvitedDetailsResponse(RoleOf(invited.Role, invited.RoleId, invited.RoleBuiltIn), invited.ReplacedInvitationId),
        MemberRemovedDetails removed => new MemberRemovedDetailsResponse(RoleOf(removed.Role, removed.RoleId, removed.RoleBuiltIn)),
        MemberRoleChangedDetails changed => new MemberRoleChangedDetailsResponse(
            RoleOf(changed.OldRole, changed.OldRoleId, changed.OldRoleBuiltIn),
            RoleOf(changed.NewRole, changed.NewRoleId, changed.NewRoleBuiltIn)),
        RoleCreatedDetails created => new RoleCreatedDetailsResponse(created.Permissions),
        RoleUpdatedDetails updated => new RoleUpdatedDetailsResponse(updated.OldName, updated.NewName, updated.PermissionsAdded, updated.PermissionsRemoved),
        TenantOwnerInvitedDetails invited => new TenantOwnerInvitedDetailsResponse(invited.InvitationId, invited.ReplacedInvitationId),
        TenantOwnerMadeDetails made => new TenantOwnerMadeDetailsResponse(
            made.UserId,
            entry.DetailsUserName,
            entry.DetailsUserDeleted ?? false,
            RoleOf(made.OldRole, made.OldRoleId, made.OldRoleBuiltIn)),
        TenantPurgeWarnedDetails warned => new TenantPurgeWarnedDetailsResponse(warned.PurgesAt),
        TenantRenamedDetails renamed => new TenantRenamedDetailsResponse(renamed.OldName, renamed.NewName),
        TenantSuspendedDetails suspended => new TenantSuspendedDetailsResponse(suspended.Reason),
        var details => throw new ArgumentOutOfRangeException(nameof(entry), details.GetType().Name, "The response has no details of this type."),
    };

    // An entry keeps the role as it was at that moment (K3).
    private static AuditRoleResponse RoleOf(string name, Guid id, bool builtIn) => new(id, name, builtIn);

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
