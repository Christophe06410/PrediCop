using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public DashboardDto Dashboard { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            Dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/dashboard") ?? GetFakeDashboard();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le dashboard depuis l'API.");
            Dashboard = GetFakeDashboard();
        }

        return Page();
    }

    private static DashboardDto GetFakeDashboard()
    {
        var now = DateTime.Now;
        return new DashboardDto
        {
            CallsToday = 17,
            ActiveMissions = 4,
            AvailableVehicles = 3,
            VehiclesOnMission = 4,
            MissionsByHour = Enumerable.Range(6, 17).Select(h => new HourlyMissionCount
            {
                Hour = h,
                Count = h switch
                {
                    6 or 7 => Random.Shared.Next(0, 2),
                    8 or 9 => Random.Shared.Next(1, 4),
                    10 or 11 or 12 => Random.Shared.Next(2, 6),
                    13 or 14 => Random.Shared.Next(1, 5),
                    15 or 16 or 17 => Random.Shared.Next(2, 7),
                    18 or 19 or 20 => Random.Shared.Next(3, 8),
                    21 or 22 => Random.Shared.Next(1, 5),
                    _ => 0
                }
            }).ToList(),
            TopVehicles = new List<VehicleStats>
            {
                new() { CallSign = "PM-01", AcceptedCount = 8, RefusedCount = 1, TotalCount = 9 },
                new() { CallSign = "PM-03", AcceptedCount = 6, RefusedCount = 2, TotalCount = 8 },
                new() { CallSign = "PM-02", AcceptedCount = 5, RefusedCount = 3, TotalCount = 8 },
                new() { CallSign = "PM-05", AcceptedCount = 4, RefusedCount = 0, TotalCount = 4 },
                new() { CallSign = "PM-04", AcceptedCount = 2, RefusedCount = 4, TotalCount = 6 },
            },
            RecentMissions = new List<MissionDto>
            {
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-017", Status = "InProgress", TargetAddress = "12 rue de la Paix", AssignedVehicleCallSign = "PM-01", CreatedAt = now.AddMinutes(-10) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-016", Status = "Accepted", TargetAddress = "Place du Marché", AssignedVehicleCallSign = "PM-03", CreatedAt = now.AddMinutes(-20) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-015", Status = "Completed", TargetAddress = "Avenue Gambetta", AssignedVehicleCallSign = "PM-02", CreatedAt = now.AddMinutes(-60), CompletedAt = now.AddMinutes(-5) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-014", Status = "Completed", TargetAddress = "Rue Saint-Denis", AssignedVehicleCallSign = "PM-01", CreatedAt = now.AddMinutes(-90), CompletedAt = now.AddMinutes(-30) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-013", Status = "Refused", TargetAddress = "Boulevard Voltaire", AssignedVehicleCallSign = null, CreatedAt = now.AddMinutes(-120) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-012", Status = "Completed", TargetAddress = "Rue du Temple", AssignedVehicleCallSign = "PM-05", CreatedAt = now.AddHours(-2), CompletedAt = now.AddHours(-1).AddMinutes(-30) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-011", Status = "Completed", TargetAddress = "Rue de Rivoli", AssignedVehicleCallSign = "PM-03", CreatedAt = now.AddHours(-3), CompletedAt = now.AddHours(-2).AddMinutes(-20) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-010", Status = "Cancelled", TargetAddress = "Rue Beaubourg", AssignedVehicleCallSign = null, CreatedAt = now.AddHours(-4) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-009", Status = "Completed", TargetAddress = "Rue Montmartre", AssignedVehicleCallSign = "PM-02", CreatedAt = now.AddHours(-5) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-008", Status = "Completed", TargetAddress = "Rue de la Roquette", AssignedVehicleCallSign = "PM-04", CreatedAt = now.AddHours(-6) },
            }
        };
    }
}
