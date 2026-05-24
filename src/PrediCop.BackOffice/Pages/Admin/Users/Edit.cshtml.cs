using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Users;

[Authorize(Roles = "Admin,Manager")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    [BindProperty]
    public EditUserDto Input { get; set; } = new();

    public bool IsEdit => Input.Id != Guid.Empty;

    public static readonly List<string> Roles = ["Operator", "Officer", "PatrolLeader", "PatrolAgent", "Verbalisateur", "Manager", "Admin"];

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var user = await client.GetFromJsonAsync<UserDto>($"/api/users/{id}");
            if (user is null) return NotFound();

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

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Password required only on create
        if (!IsEdit && string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", "Le mot de passe est obligatoire pour un nouvel utilisateur.");

        if (!ModelState.IsValid) return Page();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            HttpResponseMessage response;

            if (IsEdit)
            {
                var req = new
                {
                    Input.FirstName,
                    Input.LastName,
                    Input.Email,
                    Input.BadgeNumber,
                    Input.Role,
                    Input.IsActive,
                    Password = string.IsNullOrWhiteSpace(Input.Password) ? null : Input.Password
                };
                response = await client.PutAsJsonAsync($"/api/users/{Input.Id}", req);
            }
            else
            {
                var req = new
                {
                    Input.FirstName,
                    Input.LastName,
                    Input.Email,
                    Input.BadgeNumber,
                    Password = Input.Password!,
                    Input.Role,
                    Input.IsActive
                };
                response = await client.PostAsJsonAsync("/api/users", req);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("API error {Status}: {Body}", (int)response.StatusCode, body);
                ModelState.AddModelError(string.Empty, $"Erreur API ({(int)response.StatusCode}). Vérifiez les données.");
                return Page();
            }

            TempData["SuccessMessage"] = IsEdit
                ? $"Utilisateur {Input.FirstName} {Input.LastName} mis à jour."
                : $"Utilisateur {Input.FirstName} {Input.LastName} créé avec succès.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la sauvegarde de l'utilisateur");
            ModelState.AddModelError(string.Empty, "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.");
            return Page();
        }

        return RedirectToPage("/Admin/Users/Index");
    }
}
