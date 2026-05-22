using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class ImpoundedVehicle : TenantEntity
{
    /// <summary>Immatriculation</summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>Marque (Renault, Peugeot…)</summary>
    public string Make { get; set; } = string.Empty;

    /// <summary>Modèle</summary>
    public string Model { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public VehicleCategory Category { get; set; }

    /// <summary>Motif d'enlèvement</summary>
    public ImpoundReason Reason { get; set; }

    public DateTime ImpoundedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Agent qui a procédé à l'enlèvement</summary>
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    /// <summary>Adresse où le véhicule a été trouvé</summary>
    public string OriginalAddress { get; set; } = string.Empty;

    /// <summary>Lieu de stockage (ex: "Fourrière municipale ZAC Nord")</summary>
    public string StorageLocation { get; set; } = string.Empty;

    /// <summary>Position GPS si disponible</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>État constaté à l'enlèvement</summary>
    public string? ConditionNotes { get; set; }

    /// <summary>JSON array de chemins/URLs photos</summary>
    public string? PhotoUrls { get; set; }

    public ImpoundStatus Status { get; set; } = ImpoundStatus.InStorage;

    public DateTime? ReleasedAt { get; set; }

    /// <summary>Nom du propriétaire qui récupère le véhicule</summary>
    public string? ReleasedToName { get; set; }

    /// <summary>Numéro de pièce d'identité du propriétaire</summary>
    public string? ReleasedToIdNumber { get; set; }

    public DateTime? DestroyedAt { get; set; }

    public string? Notes { get; set; }
}
