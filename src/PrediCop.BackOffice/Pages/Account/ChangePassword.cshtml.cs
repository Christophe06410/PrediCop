using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Account;

[Authorize]
public class ChangePasswordModel(IHttpClientFactory httpClientFactory, ILogger<ChangePasswordModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Le mot de passe actuel est obligatoire.")]
    [Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Le nouveau mot de passe est obligatoire.")]
    [MinLength(8, ErrorMessage = "Le nouveau mot de passe doit faire au moins 8 caractères.")]
    [Display(Name = "Nouveau mot de passe")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La confirmation est obligatoire.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/auth/change-password", new
            {
                CurrentPassword,
                NewPassword
            });

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Mot de passe modifié avec succès.";
                return RedirectToPage("/Index");
            }

            var body = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Change password error {Status}: {Body}", (int)response.StatusCode, body);

            ErrorMessage = (int)response.StatusCode == 400
                ? "Mot de passe actuel incorrect."
                : "Impossible de modifier le mot de passe. Veuillez réessayer.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du changement de mot de passe");
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
        }

        return Page();
    }
}
