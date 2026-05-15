namespace PrediCop.Core.DTOs;

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
