using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class ShiftScheduleConfiguration : IEntityTypeConfiguration<ShiftSchedule>
{
    public void Configure(EntityTypeBuilder<ShiftSchedule> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => new { s.TenantId, s.AgentId, s.Date });
        builder.HasIndex(s => new { s.TenantId, s.Date });

        builder.HasOne(s => s.Agent)
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Vehicle)
            .WithMany()
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
