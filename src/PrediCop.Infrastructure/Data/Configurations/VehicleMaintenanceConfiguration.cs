using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;

namespace PrediCop.Infrastructure.Data.Configurations;

public class VehicleMaintenanceConfiguration : IEntityTypeConfiguration<VehicleMaintenance>
{
    public void Configure(EntityTypeBuilder<VehicleMaintenance> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.ProviderName)
            .HasMaxLength(200);

        builder.Property(m => m.Notes)
            .HasMaxLength(1000);

        builder.Property(m => m.Cost)
            .HasColumnType("decimal(10,2)");

        builder.Ignore(m => m.IsOverdue);
        builder.Ignore(m => m.IsUpcoming);

        builder.HasOne(m => m.Vehicle)
            .WithMany()
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.TenantId, m.VehicleId, m.ScheduledDate });
    }
}
