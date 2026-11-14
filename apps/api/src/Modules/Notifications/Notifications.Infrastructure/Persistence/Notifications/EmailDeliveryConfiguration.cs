using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.Notifications;

internal sealed class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
{
    public void Configure(EntityTypeBuilder<EmailDelivery> delivery)
    {
        delivery.ToTable("email_deliveries");
        delivery.HasKey(d => d.Id);
        delivery.Property(d => d.Id).ValueGeneratedNever();
        delivery.HasIndex(d => d.UserId);
    }
}
