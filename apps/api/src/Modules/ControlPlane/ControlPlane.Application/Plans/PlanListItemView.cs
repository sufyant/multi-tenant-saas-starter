namespace ControlPlane.Application.Plans;

/// <summary>A plan in the platform's list: its name, whether it is the default plan, and how many tenants are on it.</summary>
public sealed record PlanListItemView(Guid Id, string Name, bool IsDefault, int TenantCount, DateTimeOffset CreatedAt);
