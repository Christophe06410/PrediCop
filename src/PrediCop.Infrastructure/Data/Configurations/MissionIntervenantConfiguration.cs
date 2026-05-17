using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class MissionIntervenantConfiguration : IEntityTypeConfiguration<MissionIntervenant>
{
    public void Configure(EntityTypeBuilder<MissionIntervenant> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FullName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Role).HasMaxLength(100);
        builder.Property(i => i.PhoneNumber).HasMaxLength(30);
        builder.Property(i => i.Notes).HasMaxLength(2000);

        builder.HasOne(i => i.Mission)
            .WithMany(m => m.Intervenants)
            .HasForeignKey(i => i.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => new { i.MissionId, i.Order });
    }
}
