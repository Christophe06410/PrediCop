using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        builder.HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Vehicles)
            .WithOne(v => v.Tenant)
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Streets)
            .WithOne(s => s.Tenant)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.SubscriptionStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.SubscriptionPlan).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.SubscriptionPeriod).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.StripeCustomerId).HasMaxLength(200);
        builder.Property(t => t.StripeSubscriptionId).HasMaxLength(200);
        builder.Property(t => t.StripeCheckoutSessionId).HasMaxLength(200);
        builder.HasIndex(t => t.StripeCustomerId);

        builder.Property(t => t.DpoEmail).HasMaxLength(256);
    }
}
