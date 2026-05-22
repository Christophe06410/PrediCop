using System.ComponentModel.DataAnnotations.Schema;
using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class VehicleMaintenance : TenantEntity
{
    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;

    public MaintenanceType Type { get; set; }

    /// <summary>Date prévue de l'entretien.</summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>Date réelle d'exécution (si effectué).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Kilométrage au moment de l'entretien.</summary>
    public int? KmAtService { get; set; }

    /// <summary>Description de l'entretien.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Coût en euros.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Garage / prestataire.</summary>
    public string? ProviderName { get; set; }

    public string? Notes { get; set; }

    public bool IsCompleted { get; set; } = false;

    /// <summary>Entretien en retard (calculé, non persisté en DB).</summary>
    [NotMapped]
    public bool IsOverdue => !IsCompleted && ScheduledDate < DateTime.UtcNow;

    /// <summary>Entretien à venir dans les 30 prochains jours (calculé, non persisté en DB).</summary>
    [NotMapped]
    public bool IsUpcoming => !IsCompleted && ScheduledDate >= DateTime.UtcNow && ScheduledDate < DateTime.UtcNow.AddDays(30);
}
