using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class ElectronicTicket : TenantEntity
{
    /// <summary>Numéro séquentiel généré à la création, ex: "PV-2026-00001"</summary>
    public string TicketNumber { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public Guid IssuedById { get; set; }
    public User IssuedBy { get; set; } = null!;

    public string IssuedAtAddress { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Immatriculation du véhicule verbalisé</summary>
    public string PlateNumber { get; set; } = string.Empty;

    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleColor { get; set; }

    public InfractionType InfractionType { get; set; }

    /// <summary>Article du code de la route ou réglementation municipale (ex: "R417-9")</summary>
    public string? ArticleCode { get; set; }

    /// <summary>Montant de l'amende en euros</summary>
    public decimal FineAmount { get; set; }

    public string? Notes { get; set; }

    /// <summary>JSON array de chemins de photos</summary>
    public string? PhotoUrls { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Issued;

    public bool IsSigned { get; set; }
    public DateTime? SignedAt { get; set; }

    public bool ExportedToAntai { get; set; }
    public DateTime? ExportedAt { get; set; }

    /// <summary>Mission liée à ce PV (optionnel)</summary>
    public Guid? MissionId { get; set; }
    public Mission? Mission { get; set; }
}
