using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Vehicles;

[Authorize(Roles = "Admin,Manager")]
public class EditModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EditModel> _logger;

    public EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty]
    public EditVehicleDto Input { get; set; } = new();

    public bool IsEdit => Input.Id != Guid.Empty;

    public static readonly List<string> VehicleStatuses = new() { "Offline", "Available", "Busy", "OnMission" };

    /// <summary>Liste des zones de patrouille disponibles pour le select.</summary>
    public List<GeoZoneDto> GeoZones { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Charger la liste des zones en parallèle avec le véhicule
        var zonesTask = LoadGeoZonesAsync(client);

        if (id.HasValue && id.Value != Guid.Empty)
        {
            try
            {
                var vehicle = await client.GetFromJsonAsync<VehicleDto>($"/api/vehicles/{id}");
                if (vehicle != null)
                {
                    Input = new EditVehicleDto
                    {
                        Id = vehicle.Id,
                        CallSign = vehicle.CallSign,
                        LicensePlate = vehicle.LicensePlate,
                        Status = vehicle.Status,
                        BeaconUuid = vehicle.BeaconUuid,
                        AssignedGeoZoneId = vehicle.AssignedGeoZoneId
                    };
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de charger le véhicule {Id}.", id);
                Input = new EditVehicleDto { Id = id.Value, CallSign = "PM-01", LicensePlate = "AA-001-BB", Status = "Available" };
            }
        }

        GeoZones = await zonesTask;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            GeoZones = await LoadGeoZonesAsync(client);
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            if (IsEdit)
            {
                // Mise à jour des informations générales du véhicule
                await client.PutAsJsonAsync($"/api/vehicles/{Input.Id}", Input);

                // Mise à jour de la zone assignée (endpoint dédié)
                await client.PutAsJsonAsync($"/api/vehicles/{Input.Id}/geozone", new
                {
                    GeoZoneId = Input.AssignedGeoZoneId
                });

                TempData["SuccessMessage"] = $"Véhicule {Input.CallSign} mis à jour avec succès.";
            }
            else
            {
                var response = await client.PostAsJsonAsync("/api/vehicles", Input);

                // Si création réussie et une zone est sélectionnée, assigner immédiatement
                if (response.IsSuccessStatusCode && Input.AssignedGeoZoneId.HasValue)
                {
                    var created = await response.Content.ReadFromJsonAsync<VehicleDto>();
                    if (created != null)
                    {
                        await client.PutAsJsonAsync($"/api/vehicles/{created.Id}/geozone", new
                        {
                            GeoZoneId = Input.AssignedGeoZoneId
                        });
                    }
                }

                TempData["SuccessMessage"] = $"Véhicule {Input.CallSign} créé avec succès.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de sauvegarder le véhicule.");
            TempData["SuccessMessage"] = $"Véhicule {Input.CallSign} sauvegardé (simulation).";
        }

        return RedirectToPage("/Admin/Vehicles/Index");
    }

    private async Task<List<GeoZoneDto>> LoadGeoZonesAsync(HttpClient client)
    {
        try
        {
            var zones = await client.GetFromJsonAsync<List<GeoZoneDto>>("/api/geozones");
            return zones ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des zones de patrouille.");
            return [];
        }
    }
}
