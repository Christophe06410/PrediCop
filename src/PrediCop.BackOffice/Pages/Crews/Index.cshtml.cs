using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Crews;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<CrewSheetEntryDto> Crews { get; set; } = [];
    public List<AvailableAgentDto> AvailableAgents { get; set; } = [];
    public DateTime LoadedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        LoadedAt = DateTime.Now;
        var client = _httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var crewTask   = client.GetFromJsonAsync<List<CrewSheetEntryDto>>("/api/vehicles/crew-sheet?includeOffline=true", ct);
            var agentTask  = client.GetFromJsonAsync<List<AvailableAgentDto>>("/api/patrol/available-agents", ct);
            await Task.WhenAll(crewTask, agentTask);
            Crews           = crewTask.IsCompletedSuccessfully  ? crewTask.Result  ?? [] : [];
            AvailableAgents = agentTask.IsCompletedSuccessfully ? agentTask.Result ?? [] : [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de charger la fiche équipages depuis l'API.");
            ErrorMessage = "Impossible de charger les équipages. Vérifiez que l'API est démarrée.";
        }

        return Page();
    }
}

public class CrewSheetEntryDto
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string? Indicatif { get; set; }
    public DateTime? SessionStartedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public List<CrewMemberDto> Officers { get; set; } = [];
    public ActiveMissionDto? CurrentMission { get; set; }
}

public class CrewMemberDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
}

public class ActiveMissionDto
{
    public Guid MissionId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
    public DateTime? AcceptedAt { get; set; }
}

public class AvailableAgentDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
