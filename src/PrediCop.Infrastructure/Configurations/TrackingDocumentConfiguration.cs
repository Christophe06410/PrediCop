using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Configurations;

public class TrackingDocumentConfiguration : IEntityTypeConfiguration<TrackingDocument>
{
    public void Configure(EntityTypeBuilder<TrackingDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Reference).IsRequired().HasMaxLength(50);
        builder.HasIndex(d => new { d.TenantId, d.Reference }).IsUnique();

        builder.Property(d => d.Title).IsRequired().HasMaxLength(300);

        builder.HasOne(d => d.Mission)
            .WithMany()
            .HasForeignKey(d => d.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Entries)
            .WithOne(e => e.Document)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.TenantId, d.MissionId });
        builder.HasIndex(d => new { d.TenantId, d.Status });
    }
}
