namespace PrediCop.BackOffice.Models;

public class TimeSeriesStatsResponse
{
    public List<DayStats> Days { get; set; } = [];
    public PeriodStatsResponse CurrentPeriod { get; set; } = new();
    public PeriodStatsResponse PreviousPeriod { get; set; } = new();
}

public class PeriodStatsResponse
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public int TotalCalls { get; set; }
    public int ClosedCalls { get; set; }
    public int CallsWithMission { get; set; }

    public int TotalMissions { get; set; }
    public int CompletedMissions { get; set; }
    public int RefusedMissions { get; set; }
    public double AverageResponseTimeMinutes { get; set; }

    public double? CallsDeltaPercent { get; set; }
    public double? MissionsDeltaPercent { get; set; }
    public double? ResponseTimeDeltaPercent { get; set; }
}

public class DayStats
{
    public DateTime Date { get; set; }
    public int Calls { get; set; }
    public int Missions { get; set; }
    public int CompletedMissions { get; set; }
    public double AverageResponseTimeMinutes { get; set; }
}

public class DashboardDto
{
    public int CallsToday { get; set; }
    public int ActiveMissions { get; set; }
    public int AvailableVehicles { get; set; }
    public int VehiclesOnMission { get; set; }
    public List<HourlyMissionCount> MissionsByHour { get; set; } = [];
    public List<VehicleStats> TopVehicles { get; set; } = [];
    public List<MissionDto> RecentMissions { get; set; } = [];
}

public class HourlyMissionCount
{
    public int Hour { get; set; }
    public int Count { get; set; }
}

public class VehicleStats
{
    public string CallSign { get; set; } = string.Empty;
    public int AcceptedCount { get; set; }
    public int RefusedCount { get; set; }
    public int TotalCount { get; set; }
}
