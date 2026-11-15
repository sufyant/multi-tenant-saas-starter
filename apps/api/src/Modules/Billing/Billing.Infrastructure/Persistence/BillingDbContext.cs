using Billing.Domain.StripeEvents;
using Billing.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Tenancy;
using Wolverine;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// The <c>billing</c> schema: each tenant's subscription, under row level security, and the provider's events that billing received, which
/// belong to no tenant (R6).
/// </summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "billing";

    internal DbSet<Subscription> Subscriptions => Set<Subscription>();

    internal DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // One row for each tenant, keyed by the tenant column, which TenantDbContext gives every tenant entity. R15: an interval and a status
        // of the kinds the code knows. The provider's ids are unique, so a customer or a subscription belongs to one tenant.
        modelBuilder.Entity<Subscription>(subscription =>
        {
            subscription.ToTable("subscriptions", table =>
            {
                table.HasCheckConstraint("ck_subscriptions_interval", CheckConstraints.OneOf<BillingInterval>("interval"));
                table.HasCheckConstraint("ck_subscriptions_status", CheckConstraints.OneOf<SubscriptionStatus>("status"));
            });
            subscription.Property<Guid>("TenantId");
            subscription.HasKey("TenantId");
            subscription.Property(s => s.CustomerId).HasMaxLength(StripeEvent.IdMaxLength);
            subscription.Property(s => s.SubscriptionId).HasMaxLength(StripeEvent.IdMaxLength).IsRequired();
            subscription.Property(s => s.Interval).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            subscription.HasIndex(s => s.CustomerId).IsUnique();
            subscription.HasIndex(s => s.SubscriptionId).IsUnique();
        });

        modelBuilder.Entity<StripeEvent>(stripeEvent =>
        {
            stripeEvent.ToTable("stripe_events");
            stripeEvent.HasKey(e => e.Id);
            stripeEvent.Property(e => e.Id).HasMaxLength(StripeEvent.IdMaxLength);
            stripeEvent.Property(e => e.Type).HasMaxLength(StripeEvent.TypeMaxLength);
        });
    }
}
