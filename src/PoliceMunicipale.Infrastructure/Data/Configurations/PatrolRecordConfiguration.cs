using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Infrastructure.Data.Configurations;

public class PatrolRecordConfiguration : IEntityTypeConfiguration<PatrolRecord>
{
    public void Configure(EntityTypeBuilder<PatrolRecord> builder)
    {
        builder.HasKey(pr => pr.Id);

        builder.HasIndex(pr => new { pr.StreetId, pr.PatrolledAt });
        builder.HasIndex(pr => new { pr.TenantId, pr.PatrolledAt });
    }
}
