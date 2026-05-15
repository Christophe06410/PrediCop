using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class StreetConfiguration : IEntityTypeConfiguration<Street>
{
    public void Configure(EntityTypeBuilder<Street> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.District)
            .HasMaxLength(200);

        builder.Property(s => s.City)
            .HasMaxLength(200);

        builder.Property(s => s.GeoJson)
            .HasColumnType("nvarchar(max)");

        builder.HasMany(s => s.PatrolRecords)
            .WithOne(pr => pr.Street)
            .HasForeignKey(pr => pr.StreetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.RiskEvents)
            .WithOne(re => re.Street)
            .HasForeignKey(re => re.StreetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.TenantId, s.CurrentRiskScore });
        builder.HasIndex(s => s.LastPatrolledAt);
    }
}
