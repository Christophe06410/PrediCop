using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;
using PrediCop.BackOffice.Helpers;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize]
public class DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = ApiJsonOptions.Default;

    public CallDto? Call { get; set; }

    /// <summary>Vrai si l'appel a au moins une mission terminée/refusée/annulée mais aucune active.</summary>
    public bool CanReopen { get; private set; }

    /// <summary>Vrai si au moins une mission est en cours (Proposed/Accepted/InProgress).</summary>
    public bool HasActiveMission { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}", JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'appel {Id} depuis l'API", id);
            TempData["ErrorMessage"] = "Impossible de charger les détails de l'appel.";
        }

        if (Call == null) return NotFound();

        await EnrichMissionsWithVehicleDataAsync(client);
        ComputeReopenFlags();
        return Page();
    }

    private async Task EnrichMissionsWithVehicleDataAsync(HttpClient client)
    {
        if (Call is null || !Call.Missions.Any()) return;
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles", JsonOpts) ?? [];
            var vehicleDict = vehicles.ToDictionary(v => v.CallSign, v => v, StringComparer.OrdinalIgnoreCase);

            foreach (var m in Call.Missions)
            {
                var asgn = m.Assignments
                    .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed" or "Completed");
                m.AssignedVehicleCallSign ??= asgn?.VehicleCallSign;
                m.AssignedVehicleIndicatif ??= asgn?.VehicleIndicatif;

                if (m.AssignedVehicleCallSign is not null
                    && vehicleDict.TryGetValue(m.AssignedVehicleCallSign, out var v))
                {
                    m.AssignedVehicleLicensePlate = v.LicensePlate;
                    m.AssignedVehiclePatrolType = v.PatrolType;
                    m.AssignedVehicleOfficerNames = v.OfficerNames;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les véhicules pour enrichir les missions de l'appel {Id}", Call?.Id);
        }
    }

    public async Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync($"/api/calls/{id}/create-mission", (object?)null, ct);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Nouvelle mission créée avec succès. Le dispatch automatique est en cours.";
                return RedirectToPage("/Missions/Index");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Échec création mission reprise pour l'appel {Id} : {Status} — {Body}",
                id, (int)response.StatusCode, body);
            TempData["ErrorMessage"] = (int)response.StatusCode == 400
                ? "Impossible de reprendre : une mission est déjà active sur cet appel."
                : $"Erreur lors de la création de la mission ({(int)response.StatusCode}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la reprise de l'appel {Id}", id);
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }

        return RedirectToPage(new { id });
    }

    private void ComputeReopenFlags()
    {
        if (Call is null) return;

        HasActiveMission = Call.Missions.Any(m =>
            m.Status is "Pending" or "Proposed" or "Accepted" or "InProgress");

        CanReopen = Call.Missions.Any() && !HasActiveMission;
    }
}
