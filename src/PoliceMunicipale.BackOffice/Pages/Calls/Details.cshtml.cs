using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PoliceMunicipale.BackOffice.Models;
using System.Net.Http.Json;

namespace PoliceMunicipale.BackOffice.Pages.Calls;

public class DetailsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public CallDto? Call { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PoliceMunicipaleApi");
            Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger l'appel {Id} depuis l'API.", id);
            Call = GetFakeCall(id);
        }

        if (Call == null) return NotFound();
        return Page();
    }

    private static CallDto GetFakeCall(Guid id) => new()
    {
        Id = id,
        Reference = "APP-2026-001",
        ReceivedAt = DateTime.Now.AddHours(-2),
        Status = "MissionCreated",
        CallerName = "M. Dupont Pierre",
        CallerPhone = "06 12 34 56 78",
        IncidentCategory = "Tapage",
        IncidentDescription = "Tapage nocturne au 3ème étage, musique très forte depuis 22h. Les voisins ont tenté de contacter le locataire sans succès.",
        IncidentAddress = "12 rue de la Paix",
        IncidentAddressComplement = "Bâtiment B, 3ème étage",
        IncidentLatitude = 48.8698,
        IncidentLongitude = 2.3309,
        ThirdParties = "M. Durand (voisin du dessus)",
        InternalNotes = "Récidiviste - 3ème appel ce mois-ci pour la même adresse.",
        OperatorName = "Opérateur Centrale",
        Missions = new List<MissionDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Reference = "MSS-2026-001",
                Status = "Accepted",
                TargetAddress = "12 rue de la Paix",
                TargetLatitude = 48.8698,
                TargetLongitude = 2.3309,
                BriefingText = "Intervenir pour tapage nocturne. 3ème étage Bâtiment B.",
                CreatedAt = DateTime.Now.AddHours(-1.9),
                AcceptedAt = DateTime.Now.AddHours(-1.8),
                AssignedVehicleCallSign = "PM-01",
                Assignments = new List<MissionAssignmentDto>
                {
                    new() { Id = Guid.NewGuid(), VehicleCallSign = "PM-01", ProposalOrder = 1, Status = "Accepted", ProposedAt = DateTime.Now.AddHours(-1.9), RespondedAt = DateTime.Now.AddHours(-1.8), DistanceAtProposal = 1.2 }
                }
            }
        }
    };
}
