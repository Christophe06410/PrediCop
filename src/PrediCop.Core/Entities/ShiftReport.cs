namespace PrediCop.Core.Entities;

public class ShiftReport : TenantEntity
{
    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;

    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }

    public string OfficerNames { get; set; } = string.Empty; // "Jean Dupont, Marie Martin"

    public int MissionCount { get; set; }
    public int CompletedMissionCount { get; set; }
    public int RefusedMissionCount { get; set; }

    public int PatrolRecordCount { get; set; } // nombre de passages de rues
    public double EstimatedKm { get; set; }    // km estimés (patrolRecords * 0.5km par rue)

    public int DocumentCount { get; set; }     // TrackingDocuments créés pendant la vacation

    public string? Notes { get; set; }         // commentaires libres de l'agent

    public bool IsSigned { get; set; }         // signature électronique
    public DateTime? SignedAt { get; set; }
}
