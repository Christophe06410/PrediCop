namespace PoliceMunicipale.BackOffice.Models;

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
