using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Reference)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(m => new { m.TenantId, m.Reference })
            .IsUnique();

        builder.Property(m => m.TargetAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.BriefingText)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.CompletionReport).HasMaxLength(4000);
        builder.Property(m => m.LocationDetail).HasMaxLength(500);
        builder.Property(m => m.NarrativeReport).HasMaxLength(8000);

        builder.HasMany(m => m.Assignments)
            .WithOne(ma => ma.Mission)
            .HasForeignKey(ma => ma.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.MediaAttachments)
            .WithOne(ma => ma.Mission)
            .HasForeignKey(ma => ma.MissionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => new { m.TenantId, m.Status });
    }
}
