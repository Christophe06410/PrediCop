using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;

namespace PrediCop.BackOffice.Pages.Account;

[AllowAnonymous]
public class TwoFactorModel(IHttpClientFactory httpClientFactory, ILogger<TwoFactorModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions HttpJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        var tempToken = HttpContext.Session.GetString("TempTotpToken");
        if (string.IsNullOrEmpty(tempToken))
            return RedirectToPage("/Account/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        var tempToken = HttpContext.Session.GetString("TempTotpToken");
        if (string.IsNullOrEmpty(tempToken))
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Veuillez saisir votre code TOTP.";
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApiAnon");
            var response = await client.PostAsJsonAsync("/api/auth/2fa/verify",
                new TotpVerifyRequest(tempToken, Code.Trim()),
                HttpJsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Code invalide ou expiré. Veuillez réessayer.";
                return Page();
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(HttpJsonOptions, ct);
            if (result is null || string.IsNullOrEmpty(result.AccessToken) || result.User is null)
            {
                ErrorMessage = "Réponse inattendue du serveur.";
                return Page();
            }

            // Nettoyer le token temporaire de la session
            HttpContext.Session.Remove("TempTotpToken");

            HttpContext.Session.SetString("JwtToken", result.AccessToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new(ClaimTypes.Name, result.User.FullName),
                new(ClaimTypes.Email, result.User.Email),
                new(ClaimTypes.Role, result.User.Role.ToString()),
                new("tenantId", result.User.TenantId.ToString()),
                new("tenantSlug", result.User.TenantSlug),
                new("tenantName", result.User.TenantName),
                new("userId", result.User.Id.ToString()),
                new("badgeNumber", result.User.BadgeNumber)
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Redirect(returnUrl ?? "/");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la vérification 2FA");
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
            return Page();
        }
    }
}
