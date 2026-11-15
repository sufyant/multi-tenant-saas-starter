namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The plan and the interval whose move the member looks at before they confirm it.</summary>
public sealed record PlanChangePreviewRequest(Guid PlanId, PlanChangeRequestInterval Interval);
