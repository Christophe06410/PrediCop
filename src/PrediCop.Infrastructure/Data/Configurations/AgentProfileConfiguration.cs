using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.BloodType)
            .HasMaxLength(10);

        builder.Property(p => p.EmergencyContact1Name)
            .HasMaxLength(200);

        builder.Property(p => p.EmergencyContact1Phone)
            .HasMaxLength(50);

        builder.Property(p => p.EmergencyContact1Relationship)
            .HasMaxLength(100);

        builder.Property(p => p.EmergencyContact2Name)
            .HasMaxLength(200);

        builder.Property(p => p.EmergencyContact2Phone)
            .HasMaxLength(50);

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(p => new { p.TenantId, p.AgentId })
            .IsUnique();

        builder.HasOne(p => p.Agent)
            .WithMany()
            .HasForeignKey(p => p.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
