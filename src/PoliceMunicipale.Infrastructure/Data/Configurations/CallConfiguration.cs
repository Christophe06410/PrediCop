using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Infrastructure.Data.Configurations;

public class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Reference)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => new { c.TenantId, c.Reference })
            .IsUnique();

        builder.Property(c => c.CallerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.CallerPhone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.IncidentDescription)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.IncidentCategory)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.IncidentAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.IncidentAddressComplement)
            .HasMaxLength(200);

        builder.Property(c => c.ThirdParties)
            .HasMaxLength(2000);

        builder.Property(c => c.Notes)
            .HasMaxLength(2000);

        builder.Property(c => c.InternalNotes)
            .HasMaxLength(2000);

        builder.HasOne(c => c.Operator)
            .WithMany()
            .HasForeignKey(c => c.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Missions)
            .WithOne(m => m.Call)
            .HasForeignKey(m => m.CallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => c.ReceivedAt);
    }
}
