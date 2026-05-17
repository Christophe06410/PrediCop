using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.GeoZones;

[Authorize(Roles = "Admin,Manager")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string Color { get; set; } = "#3b82f6";
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public string VerticesJson { get; set; } = "[]";

    public Guid? ZoneId { get; set; }
    public string ExistingVerticesJson { get; set; } = "[]";

    public async Task OnGetAsync(Guid? id)
    {
        ZoneId = id;
        if (id == null) return;

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var zone = await client.GetFromJsonAsync<GeoZoneDto>($"/api/geozones/{id}", JsonOpts);
            if (zone == null) return;

            Name = zone.Name;
            Description = zone.Description;
            Color = zone.Color;
            IsActive = zone.IsActive;
            ExistingVerticesJson = JsonSerializer.Serialize(zone.Vertices);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la zone {Id}", id);
            TempData["ErrorMessage"] = "Impossible de charger la zone.";
        }
    }

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");

        List<VertexDto> vertices;
        try { vertices = JsonSerializer.Deserialize<List<VertexDto>>(VerticesJson, JsonOpts) ?? []; }
        catch { vertices = []; }

        try
        {
            if (id == null)
            {
                var body = new
                {
                    name = Name,
                    description = Description,
                    color = Color,
                    vertices
                };
                var resp = await client.PostAsJsonAsync("/api/geozones", body);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Erreur lors de la création de la zone.";
                    return Page();
                }
                TempData["SuccessMessage"] = "Zone créée avec succès.";
            }
            else
            {
                var body = new
                {
                    name = Name,
                    description = Description,
                    color = Color,
                    isActive = IsActive,
                    vertices
                };
                var resp = await client.PutAsJsonAsync($"/api/geozones/{id}", body);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Erreur lors de la mise à jour.";
                    return Page();
                }
                TempData["SuccessMessage"] = "Zone mise à jour.";
            }

            return RedirectToPage("/Admin/GeoZones/Index");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur sauvegarde zone");
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
            return Page();
        }
    }

    public class GeoZoneDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "#3b82f6";
        public bool IsActive { get; set; }
        public List<VertexDto> Vertices { get; set; } = [];
    }

    public class VertexDto
    {
        public int Order { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
