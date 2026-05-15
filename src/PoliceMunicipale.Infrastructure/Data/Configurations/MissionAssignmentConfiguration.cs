using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Infrastructure.Data.Configurations;

public class MissionAssignmentConfiguration : IEntityTypeConfiguration<MissionAssignment>
{
    public void Configure(EntityTypeBuilder<MissionAssignment> builder)
    {
        builder.HasKey(ma => ma.Id);

        builder.Property(ma => ma.RefusalReason)
            .HasMaxLength(500);

        builder.HasIndex(ma => new { ma.MissionId, ma.ProposalOrder });
        builder.HasIndex(ma => ma.VehicleId);
    }
}
