using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<MissionDto> ActiveMissions { get; set; } = [];
    public List<MissionDto> RecentMissions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadMissionsAsync();
        return Page();
    }

    private async Task LoadMissionsAsync()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            // Active missions — dedicated endpoint returns List directly
            var active = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions/active", options);
            ActiveMissions = active ?? [];

            // Recent missions — paginated, all statuses, take last 20
            var paged = await client.GetFromJsonAsync<MissionsPage>("/api/missions?size=20&page=1", options);
            RecentMissions = (paged?.Items ?? [])
                .Where(m => m.Status is "Completed" or "Cancelled" or "Refused")
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les missions depuis l'API.");
            ActiveMissions = [];
            RecentMissions = [];
        }

        // Compute AssignedVehicleCallSign from the Assignments list
        foreach (var m in ActiveMissions.Concat(RecentMissions))
        {
            m.AssignedVehicleCallSign ??= m.Assignments
                .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
                ?.VehicleCallSign;
        }
    }

    // Minimal paged wrapper matching the API PagedResult<MissionDto> JSON shape
    private class MissionsPage
    {
        public List<MissionDto> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }
}
