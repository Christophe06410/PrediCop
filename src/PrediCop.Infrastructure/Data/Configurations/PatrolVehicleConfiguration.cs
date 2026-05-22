using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class PatrolVehicleConfiguration : IEntityTypeConfiguration<PatrolVehicle>
{
    public void Configure(EntityTypeBuilder<PatrolVehicle> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.CallSign)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(v => new { v.TenantId, v.CallSign })
            .IsUnique();

        builder.Property(v => v.LicensePlate)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(v => new { v.TenantId, v.LicensePlate })
            .IsUnique();

        builder.HasMany(v => v.Officers)
            .WithOne(vo => vo.Vehicle)
            .HasForeignKey(vo => vo.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Missions)
            .WithOne(ma => ma.Vehicle)
            .HasForeignKey(ma => ma.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.PatrolRecords)
            .WithOne(pr => pr.Vehicle)
            .HasForeignKey(pr => pr.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relation optionnelle vers la zone de patrouille assignée (géofencing)
        builder.HasOne(v => v.AssignedGeoZone)
            .WithMany(z => z.AssignedVehicles)
            .HasForeignKey(v => v.AssignedGeoZoneId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
