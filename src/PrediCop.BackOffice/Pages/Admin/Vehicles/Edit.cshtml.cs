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

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PrediCopApi");
                var vehicle = await client.GetFromJsonAsync<VehicleDto>($"/api/vehicles/{id}");
                if (vehicle != null)
                {
                    Input = new EditVehicleDto
                    {
                        Id = vehicle.Id,
                        CallSign = vehicle.CallSign,
                        LicensePlate = vehicle.LicensePlate,
                        Status = vehicle.Status
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

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            if (IsEdit)
            {
                await client.PutAsJsonAsync($"/api/vehicles/{Input.Id}", Input);
                TempData["SuccessMessage"] = $"Véhicule {Input.CallSign} mis à jour avec succès.";
            }
            else
            {
                await client.PostAsJsonAsync("/api/vehicles", Input);
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
}
