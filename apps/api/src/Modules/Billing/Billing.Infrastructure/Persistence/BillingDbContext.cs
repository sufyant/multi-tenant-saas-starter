using Billing.Domain.Checkouts;
using Billing.Domain.BillingEmails;
using Billing.Domain.Contracts;
using Billing.Domain.Customers;
using Billing.Domain.Payers;
using Billing.Domain.PriceChanges;
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

    internal DbSet<Contract> Contracts => Set<Contract>();

    internal DbSet<BillingEmail> BillingEmails => Set<BillingEmail>();

    internal DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();

    internal DbSet<PriceChange> PriceChanges => Set<PriceChange>();

    internal DbSet<Payer> Payers => Set<Payer>();

    internal DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();

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
                table.HasCheckConstraint("ck_subscriptions_scheduled_interval", CheckConstraints.OneOf<BillingInterval>("scheduled_interval"));
                table.HasCheckConstraint("ck_subscriptions_scheduled", "(scheduled_plan_id IS NULL) = (scheduled_interval IS NULL)");
            });
            subscription.Property<Guid>("TenantId");
            subscription.HasKey("TenantId");
            subscription.Property(s => s.CustomerId).HasMaxLength(StripeEvent.IdMaxLength);
            subscription.Property(s => s.SubscriptionId).HasMaxLength(StripeEvent.IdMaxLength).IsRequired();
            subscription.Property(s => s.Interval).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.ClosureEnd).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.ScheduledInterval).HasConversion<string>().HasMaxLength(20);

            // B11: the open invoice whose payment failed, all its columns empty when nothing is due.
            subscription.ComplexProperty(s => s.FailedPayment, payment =>
            {
                payment.Property(p => p.InvoiceId).HasMaxLength(StripeEvent.IdMaxLength);
                payment.Property(p => p.Currency).HasMaxLength(3);
                payment.Property(p => p.CardBrand).HasMaxLength(40);
                payment.Property(p => p.CardLast4).HasMaxLength(4);
            });
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

        // B14: a tenant's billing email, one row for each tenant that has one, keyed by the tenant column.
        modelBuilder.Entity<BillingEmail>(email =>
        {
            email.ToTable("billing_emails");
            email.Property<Guid>("TenantId");
            email.HasKey("TenantId");
            email.Property(e => e.Address).HasMaxLength(BillingEmail.MaxLength);
        });

        // B12: a tenant's Contract mark, one row for each tenant that is Contract, keyed by the tenant column.
        modelBuilder.Entity<Contract>(contract =>
        {
            contract.ToTable("contracts");
            contract.Property<Guid>("TenantId");
            contract.HasKey("TenantId");
        });

        // B5: one customer for each tenant, and a customer belongs to one tenant.
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(c => c.CustomerId);
            customer.Property(c => c.CustomerId).HasMaxLength(StripeEvent.IdMaxLength);
            customer.HasIndex(c => c.TenantId).IsUnique();
        });

        // B13: a plan's price changes and the tenants' payers, which belong to no tenant's row level security (R6).
        modelBuilder.Entity<PriceChange>(change =>
        {
            change.ToTable("price_changes");
            change.HasKey(c => c.Id);
            change.Property(c => c.PlanName).HasMaxLength(100);
            change.Property(c => c.Currency).HasMaxLength(3);
            change.ComplexProperty(c => c.OldPrices, prices =>
            {
                prices.Property(price => price.BaseMonthly).HasColumnName("old_base_monthly");
                prices.Property(price => price.PerPersonMonthly).HasColumnName("old_per_person_monthly");
                prices.Property(price => price.BaseYearly).HasColumnName("old_base_yearly");
                prices.Property(price => price.PerPersonYearly).HasColumnName("old_per_person_yearly");
            });
            change.ComplexProperty(c => c.NewPrices, prices =>
            {
                prices.Property(price => price.BaseMonthly).HasColumnName("new_base_monthly");
                prices.Property(price => price.PerPersonMonthly).HasColumnName("new_per_person_monthly");
                prices.Property(price => price.BaseYearly).HasColumnName("new_base_yearly");
                prices.Property(price => price.PerPersonYearly).HasColumnName("new_per_person_yearly");
            });
            change.Ignore(c => c.AnnouncesAt);
            change.HasIndex(c => new { c.PlanId, c.ChangedAt });
        });

        modelBuilder.Entity<Payer>(payer =>
        {
            payer.ToTable("payers");
            payer.HasKey(p => p.TenantId);
            payer.HasIndex(p => p.PlanId);
        });

        modelBuilder.Entity<ContractTenant>(listed =>
        {
            listed.ToTable("contract_tenants");
            listed.HasKey(l => l.TenantId);
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
