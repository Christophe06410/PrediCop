using Microsoft.EntityFrameworkCore;
using PoliceMunicipale.Core.Entities;

namespace PoliceMunicipale.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PatrolVehicle> PatrolVehicles => Set<PatrolVehicle>();
    public DbSet<VehicleOfficer> VehicleOfficers => Set<VehicleOfficer>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionAssignment> MissionAssignments => Set<MissionAssignment>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<PatrolRecord> PatrolRecords => Set<PatrolRecord>();
    public DbSet<StreetRiskEvent> StreetRiskEvents => Set<StreetRiskEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildIsDeletedFilter(entityType.ClrType));
            }
        }
    }

    private static System.Linq.Expressions.LambdaExpression BuildIsDeletedFilter(Type type)
    {
        var param = System.Linq.Expressions.Expression.Parameter(type, "e");
        var prop = System.Linq.Expressions.Expression.Property(param, nameof(BaseEntity.IsDeleted));
        var notDeleted = System.Linq.Expressions.Expression.Not(prop);
        return System.Linq.Expressions.Expression.Lambda(notDeleted, param);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
