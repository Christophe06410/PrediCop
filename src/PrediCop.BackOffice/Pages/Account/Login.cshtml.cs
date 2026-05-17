using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Account;

[AllowAnonymous]
public class LoginModel(IHttpClientFactory httpClientFactory, ILogger<LoginModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions HttpJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? "/");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email et mot de passe requis.";
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = Email, Password = Password },
                HttpJsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Identifiants invalides.";
                return Page();
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(HttpJsonOptions);
            if (result is null)
            {
                ErrorMessage = "Réponse inattendue du serveur.";
                return Page();
            }

            HttpContext.Session.SetString("JwtToken", result.AccessToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new(ClaimTypes.Name, result.User.FullName),
                new(ClaimTypes.Email, result.User.Email),
                new(ClaimTypes.Role, result.User.Role.ToString()),
                new("tenantId", result.User.TenantId.ToString()),
                new("userId", result.User.Id.ToString()),
                new("badgeNumber", result.User.BadgeNumber)
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Redirect(returnUrl ?? "/");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Erreur de connexion pour {Email}", Email);
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
            ErrorDetails = ex.InnerException is SocketException socketEx
                ? $"Détail: connexion refusée sur {socketEx.Message}"
                : $"Détail: {ex.Message}";
            return Page();
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout de connexion pour {Email}", Email);
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
            ErrorDetails = "Détail: délai d'attente dépassé lors de l'appel à l'API.";
            return Page();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Erreur de désérialisation de la réponse login pour {Email}", Email);
            ErrorMessage = "Réponse inattendue du serveur.";
            ErrorDetails = "Détail: format de réponse invalide reçu depuis l'API.";
            return Page();
        }
    }
}
