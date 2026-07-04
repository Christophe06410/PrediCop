namespace PrediCop.Core.Entities;

public class Street : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? City { get; set; }

    /// <summary>Zone that owns this street — null for manually-added streets.</summary>
    public Guid? GeoZoneId { get; set; }
    public GeoZone? GeoZone { get; set; }

    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
    public string? GeoJson { get; set; }

    public int BaseRiskScore { get; set; } = 5;
    public double RiskGrowthRatePerWeek { get; set; } = 1.0;
    public double NightRiskGrowthRatePerWeek { get; set; } = 2.0;
    public int NightStartHour { get; set; } = 22;
    public int NightEndHour { get; set; } = 6;
    public int CurrentRiskScore { get; set; } = 5;
    public DateTime? LastPatrolledAt { get; set; }
    public int PatrolIntervalHours { get; set; } = 24;

    // Predictive risk — algorithm-computed fields
    public int ComputedBaseRiskScore { get; set; } = 5;
    public bool IsRiskLocked { get; set; } = false;
    public int? RiskAdjustment { get; set; }
    public int? BuildingDensityScore { get; set; }
    public DateTime? BuildingDensityFetchedAt { get; set; }

    public ICollection<PatrolRecord> PatrolRecords { get; set; } = [];
    public ICollection<StreetRiskEvent> RiskEvents { get; set; } = [];
}
