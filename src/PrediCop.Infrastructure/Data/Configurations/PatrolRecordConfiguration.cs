using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data.Configurations;

public class PatrolRecordConfiguration : IEntityTypeConfiguration<PatrolRecord>
{
    public void Configure(EntityTypeBuilder<PatrolRecord> builder)
    {
        builder.HasKey(pr => pr.Id);

        builder.HasIndex(pr => new { pr.StreetId, pr.PatrolledAt });
        builder.HasIndex(pr => new { pr.TenantId, pr.PatrolledAt });
    }
}
