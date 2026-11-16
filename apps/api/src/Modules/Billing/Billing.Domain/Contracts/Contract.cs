using SharedKernel;

namespace Billing.Domain.Contracts;

/// <summary>
/// A tenant that is billed outside the app, by invoice (B12, 4.34): a system admin marked it Contract, and sets its plan. The row exists
/// while the tenant is Contract; a tenant without it pays by card. It has no card, no trial and no failed-payment states, and its members
/// open no checkout and change no plan. One row for each tenant, under the tenant's row level security; a tenant that never paid by card has
/// no customer, so the mark cannot live on the customer's row.
/// </summary>
internal sealed class Contract(DateTimeOffset startedAt, Guid startedBy) : ITenantEntity
{
    /// <summary>When the system admin marked the tenant Contract.</summary>
    public DateTimeOffset StartedAt { get; private init; } = startedAt;

    /// <summary>The user id of the system admin who marked it.</summary>
    public Guid StartedBy { get; private init; } = startedBy;
}
