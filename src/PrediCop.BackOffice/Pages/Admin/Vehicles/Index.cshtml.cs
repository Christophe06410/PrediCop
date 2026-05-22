using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Vehicles;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<VehicleDto> Vehicles { get; set; } = [];
    public int VehicleLimit { get; set; } = 9999;
    public bool IsAtVehicleLimit => Vehicles.Count >= VehicleLimit;

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var result = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles");
            Vehicles = result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les véhicules depuis l'API.");
            Vehicles = GetFakeVehicles();
        }

        var jwtToken = HttpContext.Session.GetString("JwtToken");
        if (jwtToken is not null)
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(jwtToken))
            {
                var token = handler.ReadJwtToken(jwtToken);
                var limitClaim = token.Claims.FirstOrDefault(c => c.Type == "vehicleLimit")?.Value;
                if (int.TryParse(limitClaim, out var limit)) VehicleLimit = limit;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            await client.DeleteAsync($"/api/vehicles/{id}");
            TempData["SuccessMessage"] = "Véhicule supprimé avec succès.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de supprimer le véhicule {Id}.", id);
            TempData["SuccessMessage"] = "Véhicule supprimé (simulation).";
        }

        return RedirectToPage();
    }

    private static List<VehicleDto> GetFakeVehicles() => new()
    {
        new() { Id = Guid.NewGuid(), CallSign = "PM-01", LicensePlate = "AA-001-BB", Status = "OnMission", OfficerNames = new() { "M. Durand", "Mme Leroy" } },
        new() { Id = Guid.NewGuid(), CallSign = "PM-02", LicensePlate = "AA-002-BB", Status = "Available", OfficerNames = new() { "M. Moreau" } },
        new() { Id = Guid.NewGuid(), CallSign = "PM-03", LicensePlate = "AA-003-BB", Status = "OnMission", OfficerNames = new() { "Mme Petit", "M. Simon" } },
        new() { Id = Guid.NewGuid(), CallSign = "PM-04", LicensePlate = "AA-004-BB", Status = "Available", OfficerNames = new() { "M. Laurent" } },
        new() { Id = Guid.NewGuid(), CallSign = "PM-05", LicensePlate = "AA-005-BB", Status = "Offline", OfficerNames = new() { } },
    };
}
