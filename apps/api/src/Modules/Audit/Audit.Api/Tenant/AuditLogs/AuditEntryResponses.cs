using System.Text.Json;
using Audit.Application.AuditLogs;
using SharedKernel;

namespace Audit.Api.Tenant.AuditLogs;

// The kinds keep the Domain's names (User, Tenant), so each maps to the response's enum by name.
internal static class AuditEntryResponses
{
    public static CursorPage<AuditEntryResponse> Of(CursorPage<AuditEntryView> page) => page.Map(entry => new AuditEntryResponse(
        entry.Id,
        entry.OccurredAt,
        entry.Operation,
        entry.Details is { } details ? JsonElement.Parse(details) : null,
        new AuditActorResponse(Enum.Parse<AuditActorResponseKind>(entry.Actor.Kind), entry.Actor.UserId, entry.Actor.DisplayName, entry.Actor.Deleted),
        new AuditTargetResponse(Enum.Parse<AuditTargetResponseKind>(entry.Target.Kind), entry.Target.Id, entry.Target.Name, entry.Target.Deleted)));
}
