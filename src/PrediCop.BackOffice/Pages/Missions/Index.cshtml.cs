using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Missions;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<MissionDto> ActiveMissions { get; set; } = [];
    public List<MissionDto> RecentMissions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadMissionsAsync();
        return Page();
    }

    private async Task LoadMissionsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var active = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions?active=true");
            var recent = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions?recent=true");
            ActiveMissions = active ?? [];
            RecentMissions = recent ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les missions depuis l'API.");
            ActiveMissions = GetFakeMissions(active: true);
            RecentMissions = GetFakeMissions(active: false);
        }
    }

    private static List<MissionDto> GetFakeMissions(bool active)
    {
        var now = DateTime.Now;
        if (active)
        {
            return new List<MissionDto>
            {
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-001", Status = "InProgress", TargetAddress = "12 rue de la Paix", AssignedVehicleCallSign = "PM-01", CreatedAt = now.AddMinutes(-30) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-002", Status = "Accepted", TargetAddress = "Place du Marché", AssignedVehicleCallSign = "PM-03", CreatedAt = now.AddMinutes(-15) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-003", Status = "Pending", TargetAddress = "5 Avenue Gambetta", AssignedVehicleCallSign = null, CreatedAt = now.AddMinutes(-5) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-004", Status = "Proposed", TargetAddress = "Rue Saint-Denis", AssignedVehicleCallSign = "PM-02", CreatedAt = now.AddMinutes(-2) },
            };
        }
        else
        {
            return new List<MissionDto>
            {
                new() { Id = Guid.NewGuid(), Reference = "MSS-2026-000", Status = "Completed", TargetAddress = "Boulevard Voltaire", AssignedVehicleCallSign = "PM-01", CreatedAt = now.AddHours(-2), CompletedAt = now.AddHours(-1) },
                new() { Id = Guid.NewGuid(), Reference = "MSS-2025-099", Status = "Cancelled", TargetAddress = "Rue du Temple", AssignedVehicleCallSign = null, CreatedAt = now.AddHours(-3) },
            };
        }
    }
}
