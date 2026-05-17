namespace PrediCop.Core.Entities;

/// <summary>
/// Entité de journal d'audit — ne dérive PAS de TenantEntity ni de BaseEntity
/// pour éviter le filtre global IsDeleted et être persistée indépendamment.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>TenantId nullable : null pour les actions système globales.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>UserId nullable : null pour les actions système.</summary>
    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    /// <summary>"Created", "Updated" ou "Deleted"</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Nom du type EF (ex: "Mission", "TrackingDocument")</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Id (Guid.ToString()) de l'entité affectée</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON des propriétés scalaires avant la modification (Updated/Deleted seulement)</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON des propriétés scalaires après la modification (Created/Updated seulement)</summary>
    public string? NewValues { get; set; }
}
