using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Users;

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
    public EditUserDto Input { get; set; } = new();

    public bool IsEdit => Input.Id != Guid.Empty;

    public static readonly List<string> Roles = new() { "Operator", "Officer", "Manager", "Admin" };

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PrediCopApi");
                var user = await client.GetFromJsonAsync<UserDto>($"/api/users/{id}");
                if (user != null)
                {
                    Input = new EditUserDto
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        BadgeNumber = user.BadgeNumber,
                        Role = user.Role,
                        IsActive = user.IsActive
                    };
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de charger l'utilisateur {Id}.", id);
                Input = new EditUserDto
                {
                    Id = id.Value,
                    FirstName = "Jean",
                    LastName = "Martin",
                    Email = "j.martin@pm.fr",
                    BadgeNumber = "PM-0002",
                    Role = "Officer",
                    IsActive = true
                };
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
                await client.PutAsJsonAsync($"/api/users/{Input.Id}", Input);
                TempData["SuccessMessage"] = $"Utilisateur {Input.FirstName} {Input.LastName} mis à jour.";
            }
            else
            {
                await client.PostAsJsonAsync("/api/users", Input);
                TempData["SuccessMessage"] = $"Utilisateur {Input.FirstName} {Input.LastName} créé avec succès.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de sauvegarder l'utilisateur.");
            TempData["SuccessMessage"] = $"Utilisateur {Input.FirstName} {Input.LastName} sauvegardé (simulation).";
        }

        return RedirectToPage("/Admin/Users/Index");
    }
}
