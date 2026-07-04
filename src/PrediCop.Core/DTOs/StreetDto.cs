using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class StreetResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? City { get; set; }
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
    public string? GeoJson { get; set; }
    public int BaseRiskScore { get; set; }
    public int ComputedBaseRiskScore { get; set; }
    public bool IsRiskLocked { get; set; }
    public int? RiskAdjustment { get; set; }
    public double RiskGrowthRatePerWeek { get; set; }
    public double NightRiskGrowthRatePerWeek { get; set; }
    public int NightStartHour { get; set; }
    public int NightEndHour { get; set; }
    public int CurrentRiskScore { get; set; }
    public DateTime? LastPatrolledAt { get; set; }
    public int PatrolIntervalHours { get; set; }
    public bool IsOverdue => LastPatrolledAt == null
        || (DateTime.UtcNow - LastPatrolledAt.Value).TotalHours > PatrolIntervalHours;
}

public class UpdateStreetRequest
{
    public int BaseRiskScore { get; set; }
    public double RiskGrowthRatePerWeek { get; set; }
    public double NightRiskGrowthRatePerWeek { get; set; }
    public int NightStartHour { get; set; }
    public int NightEndHour { get; set; }
    public bool IsRiskLocked { get; set; }
    public int? RiskAdjustment { get; set; }
}

public class PatrolRequest
{
    public Guid VehicleId { get; set; }
}

public class RiskEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RiskPoints { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Source { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    public DateTime? RecurrenceEndDate { get; set; }
}

public class UpdateBaseRiskRequest
{
    public int BaseRiskScore { get; set; }
}

public record RiskEventResponse(
    Guid Id,
    Guid StreetId,
    string StreetName,
    string StreetDistrict,
    string Title,
    string Description,
    int RiskPoints,
    DateTime EventDate,
    DateTime ExpiresAt,
    string Source,
    bool IsActive,
    RecurrenceType RecurrenceType,
    DateTime? RecurrenceEndDate
);

public class UpdateRiskEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RiskPoints { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Source { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    public DateTime? RecurrenceEndDate { get; set; }
}
