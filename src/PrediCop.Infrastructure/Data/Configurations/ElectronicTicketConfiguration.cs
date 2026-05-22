using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class ElectronicTicketConfiguration : IEntityTypeConfiguration<ElectronicTicket>
{
    public void Configure(EntityTypeBuilder<ElectronicTicket> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.IssuedAtAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.PlateNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.VehicleMake)
            .HasMaxLength(100);

        builder.Property(t => t.VehicleModel)
            .HasMaxLength(100);

        builder.Property(t => t.VehicleColor)
            .HasMaxLength(50);

        builder.Property(t => t.ArticleCode)
            .HasMaxLength(50);

        builder.Property(t => t.FineAmount)
            .HasPrecision(10, 2);

        builder.Property(t => t.Notes)
            .HasMaxLength(2000);

        builder.Property(t => t.PhotoUrls)
            .HasMaxLength(4000);

        // Unique ticket number per tenant
        builder.HasIndex(t => new { t.TenantId, t.TicketNumber })
            .IsUnique();

        // Common query indexes
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.IssuedAt });
        builder.HasIndex(t => new { t.TenantId, t.PlateNumber });

        // Relation avec l'agent verbalisateur
        builder.HasOne(t => t.IssuedBy)
            .WithMany()
            .HasForeignKey(t => t.IssuedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Relation optionnelle avec une mission
        builder.HasOne(t => t.Mission)
            .WithMany()
            .HasForeignKey(t => t.MissionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(t => t.MissionId);
    }
}
