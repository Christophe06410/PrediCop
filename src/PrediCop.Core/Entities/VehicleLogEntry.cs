using System.ComponentModel.DataAnnotations.Schema;

namespace PrediCop.Core.Entities;

public class VehicleLogEntry : TenantEntity
{
    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;

    public Guid OfficerId { get; set; }
    public User Officer { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>Kilométrage au départ.</summary>
    public int KmStart { get; set; }

    /// <summary>Kilométrage à l'arrivée.</summary>
    public int KmEnd { get; set; }

    /// <summary>Total kilométrique (calculé, non persisté en DB).</summary>
    [NotMapped]
    public int KmTotal => KmEnd - KmStart;

    /// <summary>Litres ajoutés si plein fait (optionnel).</summary>
    public decimal? FuelAdded { get; set; }

    /// <summary>Zone/destination principale (optionnel).</summary>
    public string? Destination { get; set; }

    public string? Notes { get; set; }
}
