using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class ImpoundedVehicleConfiguration : IEntityTypeConfiguration<ImpoundedVehicle>
{
    public void Configure(EntityTypeBuilder<ImpoundedVehicle> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.PlateNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.Make)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.Color)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.OriginalAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.StorageLocation)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.ConditionNotes)
            .HasMaxLength(2000);

        builder.Property(v => v.PhotoUrls)
            .HasMaxLength(4000);

        builder.Property(v => v.ReleasedToName)
            .HasMaxLength(200);

        builder.Property(v => v.ReleasedToIdNumber)
            .HasMaxLength(100);

        builder.Property(v => v.Notes)
            .HasMaxLength(2000);

        // Index for common queries
        builder.HasIndex(v => new { v.TenantId, v.Status });
        builder.HasIndex(v => new { v.TenantId, v.PlateNumber });

        // Relation avec l'agent
        builder.HasOne(v => v.Agent)
            .WithMany()
            .HasForeignKey(v => v.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
