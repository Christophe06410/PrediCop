using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<UserDto> Users { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var result = await client.GetFromJsonAsync<List<UserDto>>("/api/users");
            Users = result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les utilisateurs depuis l'API.");
            Users = GetFakeUsers();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            await client.PostAsync($"/api/users/{id}/toggle-active", null);
            TempData["SuccessMessage"] = "Statut de l'utilisateur mis à jour.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de modifier l'utilisateur {Id}.", id);
            TempData["SuccessMessage"] = "Statut mis à jour (simulation).";
        }

        return RedirectToPage();
    }

    private static List<UserDto> GetFakeUsers() => new()
    {
        new() { Id = Guid.NewGuid(), FirstName = "Marie", LastName = "Dupont", Email = "m.dupont@pm.fr", BadgeNumber = "PM-0001", Role = "Operator", IsActive = true },
        new() { Id = Guid.NewGuid(), FirstName = "Jean", LastName = "Martin", Email = "j.martin@pm.fr", BadgeNumber = "PM-0002", Role = "Officer", IsActive = true },
        new() { Id = Guid.NewGuid(), FirstName = "Sophie", LastName = "Bernard", Email = "s.bernard@pm.fr", BadgeNumber = "PM-0003", Role = "Officer", IsActive = true },
        new() { Id = Guid.NewGuid(), FirstName = "Luc", LastName = "Moreau", Email = "l.moreau@pm.fr", BadgeNumber = "PM-0004", Role = "Manager", IsActive = true },
        new() { Id = Guid.NewGuid(), FirstName = "Claire", LastName = "Petit", Email = "c.petit@pm.fr", BadgeNumber = "PM-0005", Role = "Admin", IsActive = false },
    };
}
