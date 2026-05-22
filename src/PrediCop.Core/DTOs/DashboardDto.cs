namespace PrediCop.Core.DTOs;

public class PeriodStatsResponse
{
    // Métadonnées période
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    // Appels
    public int TotalCalls { get; set; }
    public int ClosedCalls { get; set; }
    public int CallsWithMission { get; set; }

    // Missions
    public int TotalMissions { get; set; }
    public int CompletedMissions { get; set; }
    public int RefusedMissions { get; set; }
    public double AverageResponseTimeMinutes { get; set; }

    // Comparaison avec la période précédente (en %)
    public double? CallsDeltaPercent { get; set; }
    public double? MissionsDeltaPercent { get; set; }
    public double? ResponseTimeDeltaPercent { get; set; }
}

public class TimeSeriesStatsResponse
{
    public List<DayStats> Days { get; set; } = [];
    public PeriodStatsResponse CurrentPeriod { get; set; } = new();
    public PeriodStatsResponse PreviousPeriod { get; set; } = new();
}

public class DayStats
{
    public DateTime Date { get; set; }
    public int Calls { get; set; }
    public int Missions { get; set; }
    public int CompletedMissions { get; set; }
    public double AverageResponseTimeMinutes { get; set; }
}

public class DashboardStats
{
    public int TotalCallsToday { get; set; }
    public int OpenCalls { get; set; }
    public int ActiveMissions { get; set; }
    public int CompletedMissionsToday { get; set; }
    public int AvailableVehicles { get; set; }
    public int TotalVehicles { get; set; }
    public int HighRiskStreets { get; set; }
    public double AverageMissionResponseTimeMinutes { get; set; }
}

public class VehicleStats
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public int TotalProposed { get; set; }
    public int TotalAccepted { get; set; }
    public int TotalRefused { get; set; }
    public int TotalCompleted { get; set; }
    public double AcceptanceRate => TotalProposed == 0 ? 0 : (double)TotalAccepted / TotalProposed * 100;
}

public class MissionStats
{
    public int Hour { get; set; }
    public int MissionCount { get; set; }
    public int CompletedCount { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
