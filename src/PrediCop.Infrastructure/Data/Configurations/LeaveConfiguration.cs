using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class LeaveConfiguration : IEntityTypeConfiguration<Leave>
{
    public void Configure(EntityTypeBuilder<Leave> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Notes)
            .HasMaxLength(2000);

        builder.Property(l => l.RejectionReason)
            .HasMaxLength(500);

        builder.HasIndex(l => new { l.TenantId, l.AgentId });
        builder.HasIndex(l => new { l.TenantId, l.Status });

        builder.HasOne(l => l.Agent)
            .WithMany()
            .HasForeignKey(l => l.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
