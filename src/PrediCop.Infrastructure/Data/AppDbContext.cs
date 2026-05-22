using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PrediCop.Core.Entities;

namespace PrediCop.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // ---- Propriétés d'audit injectées par AuditContextMiddleware ----
    private Guid? _auditUserId;
    private string? _auditUserName;
    private Guid? _auditTenantId;

    /// <summary>
    /// Appelé par AuditContextMiddleware pour chaque requête authentifiée.
    /// </summary>
    public void SetAuditContext(Guid? userId, string? userName, Guid? tenantId)
    {
        _auditUserId = userId;
        _auditUserName = userName;
        _auditTenantId = tenantId;
    }

    // ---- DbSets ----
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PatrolVehicle> PatrolVehicles => Set<PatrolVehicle>();
    public DbSet<VehicleOfficer> VehicleOfficers => Set<VehicleOfficer>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionAssignment> MissionAssignments => Set<MissionAssignment>();
    public DbSet<MissionIntervenant> MissionIntervenants => Set<MissionIntervenant>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<PatrolRecord> PatrolRecords => Set<PatrolRecord>();
    public DbSet<StreetRiskEvent> StreetRiskEvents => Set<StreetRiskEvent>();
    public DbSet<TrackingDocument> TrackingDocuments => Set<TrackingDocument>();
    public DbSet<TrackingEntry> TrackingEntries => Set<TrackingEntry>();
    public DbSet<MediaAttachment> MediaAttachments => Set<MediaAttachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<GeoZone> GeoZones => Set<GeoZone>();
    public DbSet<GeoZoneVertex> GeoZoneVertices => Set<GeoZoneVertex>();
    public DbSet<ShiftReport> ShiftReports => Set<ShiftReport>();
    public DbSet<AgentQualification> AgentQualifications => Set<AgentQualification>();
    public DbSet<RgpdRequest> RgpdRequests => Set<RgpdRequest>();

    // ---- Module RH ----
    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<ShiftSchedule> ShiftSchedules => Set<ShiftSchedule>();

    // ---- Module Logistique ----
    public DbSet<EquipmentCatalog> EquipmentCatalog => Set<EquipmentCatalog>();
    public DbSet<EquipmentIssuance> EquipmentIssuances => Set<EquipmentIssuance>();
    public DbSet<UniformProfile> UniformProfiles => Set<UniformProfile>();

    // ---- Module Flotte ----
    public DbSet<VehicleLogEntry> VehicleLogEntries => Set<VehicleLogEntry>();
    public DbSet<VehicleMaintenance> VehicleMaintenances => Set<VehicleMaintenance>();

    // ---- Module Verbalisation ----
    public DbSet<ElectronicTicket> ElectronicTickets => Set<ElectronicTicket>();

    // ---- Module Fourrière ----
    public DbSet<ImpoundedVehicle> ImpoundedVehicles => Set<ImpoundedVehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<EquipmentCatalog>()
            .HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EquipmentIssuance>()
            .HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EquipmentIssuance>()
            .HasOne(e => e.Equipment)
            .WithMany()
            .HasForeignKey(e => e.EquipmentCatalogId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EquipmentIssuance>()
            .HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UniformProfile>()
            .HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UniformProfile>()
            .HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtre global IsDeleted uniquement sur TenantEntity (pas sur BaseEntity ni AuditLog)
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

        // Mise à jour UpdatedAt sur les entités modifiées
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        // Génération des AuditLogs avant le save
        AddAuditLogs(now);

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Parcourt les entrées du ChangeTracker et crée les AuditLog correspondants.
    /// Exclut les AuditLog eux-mêmes pour éviter la récursion.
    /// Les propriétés sensibles (PasswordHash) sont masquées.
    /// </summary>
    private void AddAuditLogs(DateTime timestamp)
    {
        var auditEntries = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (auditEntries.Count == 0)
            return;

        var logs = new List<AuditLog>(auditEntries.Count);

        foreach (var entry in auditEntries)
        {
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => "Unknown"
            };

            var entityName = entry.Metadata.ShortName();
            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? string.Empty;

            // TenantId de l'entité si c'est une TenantEntity
            Guid? tenantId = _auditTenantId;
            if (entry.Entity is TenantEntity te && te.TenantId != Guid.Empty)
                tenantId = te.TenantId;

            string? oldValues = null;
            string? newValues = null;

            if (action == "Updated")
            {
                oldValues = SerializeScalars(entry, useOriginalValues: true);
                newValues = SerializeScalars(entry, useOriginalValues: false);
            }
            else if (action == "Created")
            {
                newValues = SerializeScalars(entry, useOriginalValues: false);
            }
            else if (action == "Deleted")
            {
                oldValues = SerializeScalars(entry, useOriginalValues: true);
            }

            logs.Add(new AuditLog
            {
                Timestamp = timestamp,
                TenantId = tenantId,
                UserId = _auditUserId,
                UserName = _auditUserName ?? "System",
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues
            });
        }

        // Ajouter directement dans le Set pour éviter de repasser par SaveChangesAsync
        Set<AuditLog>().AddRange(logs);
    }

    private static readonly HashSet<string> RedactedProperties =
        new(StringComparer.OrdinalIgnoreCase) { "PasswordHash" };

    /// <summary>
    /// Sérialise les propriétés scalaires (pas les navigations) en JSON.
    /// Masque les valeurs sensibles.
    /// </summary>
    private static string? SerializeScalars(EntityEntry<BaseEntity> entry, bool useOriginalValues)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            // Ignorer les propriétés de navigation shadow / clés étrangères de navigation
            if (prop.Metadata.IsKey() && prop.Metadata.IsForeignKey())
                continue;

            var name = prop.Metadata.Name;
            var value = useOriginalValues ? prop.OriginalValue : prop.CurrentValue;

            if (RedactedProperties.Contains(name))
                value = "[REDACTED]";

            dict[name] = value;
        }

        if (dict.Count == 0)
            return null;

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
}
