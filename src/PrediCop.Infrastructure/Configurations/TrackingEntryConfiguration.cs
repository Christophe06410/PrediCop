using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Configurations;

public class TrackingEntryConfiguration : IEntityTypeConfiguration<TrackingEntry>
{
    public void Configure(EntityTypeBuilder<TrackingEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Content).IsRequired().HasMaxLength(4000);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.OccurredAt });
    }
}
