using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PoliceMunicipale.BackOffice.Models;
using System.Net.Http.Json;

namespace PoliceMunicipale.BackOffice.Pages.Missions;

public class DetailsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public MissionDto? Mission { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PoliceMunicipaleApi");
            Mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la mission {Id}.", id);
            Mission = GetFakeMission(id);
        }

        if (Mission == null) return NotFound();
        return Page();
    }

    private static MissionDto GetFakeMission(Guid id)
    {
        var now = DateTime.Now;
        return new MissionDto
        {
            Id = id,
            Reference = "MSS-2026-001",
            Status = "Accepted",
            CallId = Guid.NewGuid(),
            CallReference = "APP-2026-001",
            TargetAddress = "12 rue de la Paix, 75001 Paris",
            TargetLatitude = 48.8698,
            TargetLongitude = 2.3309,
            BriefingText = "Intervenir pour tapage nocturne au 3ème étage Bâtiment B. Récidiviste connnu des services.",
            CreatedAt = now.AddMinutes(-45),
            AcceptedAt = now.AddMinutes(-42),
            AssignedVehicleCallSign = "PM-01",
            Assignments = new List<MissionAssignmentDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    VehicleId = Guid.NewGuid(),
                    VehicleCallSign = "PM-04",
                    ProposalOrder = 1,
                    Status = "Refused",
                    ProposedAt = now.AddMinutes(-45),
                    RespondedAt = now.AddMinutes(-44),
                    RefusalReason = "Intervention en cours sur un autre appel",
                    DistanceAtProposal = 0.8
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    VehicleId = Guid.NewGuid(),
                    VehicleCallSign = "PM-02",
                    ProposalOrder = 2,
                    Status = "Refused",
                    ProposedAt = now.AddMinutes(-44),
                    RespondedAt = now.AddMinutes(-43),
                    RefusalReason = "Véhicule en panne",
                    DistanceAtProposal = 1.5
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    VehicleId = Guid.NewGuid(),
                    VehicleCallSign = "PM-01",
                    ProposalOrder = 3,
                    Status = "Accepted",
                    ProposedAt = now.AddMinutes(-43),
                    RespondedAt = now.AddMinutes(-42),
                    DistanceAtProposal = 2.1
                }
            }
        };
    }
}
