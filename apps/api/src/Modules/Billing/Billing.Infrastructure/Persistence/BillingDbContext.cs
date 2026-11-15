using Billing.Domain.Checkouts;
using Billing.Domain.Customers;
using Billing.Domain.StripeEvents;
using Billing.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Tenancy;
using Wolverine;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// The <c>billing</c> schema: each tenant's subscription and its open checkout, under row level security; the map from the provider's customers to their tenants,
/// which the webhook reads before any tenant is known; and the provider's events that billing received. The last two belong to no tenant's
/// row level security (R6).
/// </summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "billing";

    internal DbSet<Subscription> Subscriptions => Set<Subscription>();

    internal DbSet<Checkout> Checkouts => Set<Checkout>();

    internal DbSet<Customer> Customers => Set<Customer>();

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
                table.HasCheckConstraint("ck_subscriptions_closure_end", CheckConstraints.OneOf<ClosureEnd>("closure_end"));
            });
            subscription.Property<Guid>("TenantId");
            subscription.HasKey("TenantId");
            subscription.Property(s => s.CustomerId).HasMaxLength(StripeEvent.IdMaxLength);
            subscription.Property(s => s.SubscriptionId).HasMaxLength(StripeEvent.IdMaxLength).IsRequired();
            subscription.Property(s => s.Interval).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.ClosureEnd).HasConversion<string>().HasMaxLength(20);
            subscription.HasIndex(s => s.CustomerId).IsUnique();
            subscription.HasIndex(s => s.SubscriptionId).IsUnique();
        });

        // B7: one open checkout for each tenant, keyed by the tenant column.
        modelBuilder.Entity<Checkout>(checkout =>
        {
            checkout.ToTable("checkouts");
            checkout.Property<Guid>("TenantId");
            checkout.HasKey("TenantId");
            checkout.Property(c => c.SessionId).HasMaxLength(StripeEvent.IdMaxLength);
            checkout.Property(c => c.Url).HasMaxLength(2048);
            checkout.Property(c => c.ReturnUrl).HasMaxLength(2048);
            checkout.Property(c => c.Currency).HasMaxLength(3);
            checkout.Ignore(c => c.Order);
        });

        // B5: one customer for each tenant, and a customer belongs to one tenant.
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(c => c.CustomerId);
            customer.Property(c => c.CustomerId).HasMaxLength(StripeEvent.IdMaxLength);
            customer.HasIndex(c => c.TenantId).IsUnique();
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
