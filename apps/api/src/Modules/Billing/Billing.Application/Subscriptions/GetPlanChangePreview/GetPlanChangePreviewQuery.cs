using Billing.Application.Provider;
using SharedKernel;

namespace Billing.Application.Subscriptions.GetPlanChangePreview;

/// <summary>Asks the provider what a move at once costs today, prorated at <paramref name="ProratedAt"/> (B9, 10.04).</summary>
[MessageName("get-plan-change-preview")]
public sealed record GetPlanChangePreviewQuery(PlanChangeOrder Order, DateTimeOffset ProratedAt);
