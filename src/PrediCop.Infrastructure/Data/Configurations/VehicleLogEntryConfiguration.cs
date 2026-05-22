using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class VehicleLogEntryConfiguration : IEntityTypeConfiguration<VehicleLogEntry>
{
    public void Configure(EntityTypeBuilder<VehicleLogEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.KmStart).IsRequired();
        builder.Property(e => e.KmEnd).IsRequired();

        builder.Property(e => e.FuelAdded)
            .HasColumnType("decimal(8,2)");

        builder.Property(e => e.Destination)
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.Ignore(e => e.KmTotal);

        builder.HasOne(e => e.Vehicle)
            .WithMany()
            .HasForeignKey(e => e.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Officer)
            .WithMany()
            .HasForeignKey(e => e.OfficerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.VehicleId, e.Date });
    }
}
