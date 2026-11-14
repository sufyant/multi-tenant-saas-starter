using System.Globalization;
using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.Options;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.TermsVersions.NotifyTermsVersionCreated;

// "Our terms of use change", to every user who may accept, by email, when a version that emails the users is created, or when an edit
// of the scheduled version turns its email on. A small change emails nobody (the maintainer's decision of 2026-11-14). The text says
// whether the person accepts at their next sign-in. The system admin who acted is told nothing of their own action (U3). The deliveries go
// at the pace of the setting, because the fan-out reaches every user at once.
public static class NotifyTermsVersionCreatedHandler
{
    public const string Type = NotificationCatalog.TermsVersionCreated;

    public static async Task<OutgoingMessages> HandleAsync(
        TermsVersionCreatedEvent created,
        ITermsVersionDirectory terms,
        IUserDirectory users,
        IOptions<NotificationPaceOptions> pace,
        CancellationToken cancellationToken) =>
        created.SendsEmail
            ? await ToEveryUserAsync(
                created.EventId, created.OccurredAt, created.EffectiveDate, created.Url, created.Summary, created.RequiresAcceptance, created.CreatedBy, terms, users, pace, cancellationToken)
            : [];

    // The edit that turns the email on sends it, with the values after the edit. An edit that changes nothing of the email sends nothing.
    public static async Task<OutgoingMessages> HandleAsync(
        TermsVersionUpdatedEvent updated,
        ITermsVersionDirectory terms,
        IUserDirectory users,
        IOptions<NotificationPaceOptions> pace,
        CancellationToken cancellationToken) =>
        !updated.Old.SendsEmail && updated.New.SendsEmail
            ? await ToEveryUserAsync(
                updated.EventId,
                updated.OccurredAt,
                updated.New.EffectiveDate,
                updated.New.Url,
                updated.New.Summary,
                updated.New.RequiresAcceptance,
                updated.UpdatedBy,
                terms,
                users,
                pace,
                cancellationToken)
            : [];

    private static async Task<OutgoingMessages> ToEveryUserAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        DateOnly effectiveDate,
        string url,
        string? summary,
        bool requiresAcceptance,
        Guid actorId,
        ITermsVersionDirectory terms,
        IUserDirectory users,
        IOptions<NotificationPaceOptions> pace,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["effectiveDate"] = effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["url"] = url,
            ["nextStep"] = requiresAcceptance ? NotificationCatalog.AcceptAtSignIn : NotificationCatalog.NothingToDo,
        };
        if (summary is { } said)
        {
            values["summary"] = said;
        }

        var recipients = await users.FindContactsAsync(await terms.ListActiveUserIdsAsync(cancellationToken), cancellationToken);
        return await NotificationFanOut.ToAsync(
            Type, eventId, occurredAt, tenantId: null, values, recipients, actorId, users, cancellationToken, pace.Value.DeliveriesPerSecond);
    }
}
