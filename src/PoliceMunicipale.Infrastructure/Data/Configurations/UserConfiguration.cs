using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        builder.Property(u => u.BadgeNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => new { u.TenantId, u.BadgeNumber })
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.DeviceToken)
            .HasMaxLength(512);

        builder.Ignore(u => u.FullName);

        builder.HasMany(u => u.VehicleAssignments)
            .WithOne(vo => vo.User)
            .HasForeignKey(vo => vo.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
