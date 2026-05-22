using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;

namespace PrediCop.BackOffice.Pages.Account;

[Authorize(Roles = "Admin,Manager")]
public class Setup2FAModel(IHttpClientFactory httpClientFactory, ILogger<Setup2FAModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions HttpJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty]
    public string EnableCode { get; set; } = string.Empty;

    public TotpSetupResponse? SetupData { get; set; }
    public bool TotpAlreadyEnabled { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(bool reset = false, CancellationToken ct = default)
    {
        var statusResponse = await GetTotpStatusAsync(ct);
        if (statusResponse is null)
        {
            ErrorMessage = "Impossible de joindre le serveur.";
            return Page();
        }

        if (statusResponse.TotpEnabled && !reset)
        {
            TotpAlreadyEnabled = true;
            return Page();
        }

        // Lancer le setup (génère le secret et le QR code)
        SetupData = await CallSetupAsync(ct);
        if (SetupData is null)
            ErrorMessage = "Erreur lors de la génération du QR code.";

        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(EnableCode))
        {
            ErrorMessage = "Veuillez saisir votre code TOTP.";
            SetupData = await CallSetupAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/auth/2fa/enable",
                new TotpEnableRequest(EnableCode.Trim()),
                HttpJsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Authentification à deux facteurs activée avec succès.";
                TotpAlreadyEnabled = true;
                return Page();
            }

            ErrorMessage = "Code TOTP invalide. Veuillez réessayer.";
            SetupData = await CallSetupAsync(ct);
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'activation 2FA");
            ErrorMessage = "Impossible de joindre le serveur.";
            SetupData = await CallSetupAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDisableAsync(string disableCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disableCode))
        {
            ErrorMessage = "Veuillez saisir votre code TOTP pour désactiver la 2FA.";
            TotpAlreadyEnabled = true;
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/auth/2fa/disable",
                new TotpEnableRequest(disableCode.Trim()),
                HttpJsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Authentification à deux facteurs désactivée.";
                return Page();
            }

            ErrorMessage = "Code TOTP invalide. La 2FA n'a pas été désactivée.";
            TotpAlreadyEnabled = true;
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la désactivation 2FA");
            ErrorMessage = "Impossible de joindre le serveur.";
            TotpAlreadyEnabled = true;
            return Page();
        }
    }

    private async Task<TotpSetupResponse?> CallSetupAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsync("/api/auth/2fa/setup", null, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TotpSetupResponse>(HttpJsonOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du setup 2FA");
            return null;
        }
    }

    // DTO interne pour récupérer le statut 2FA de l'utilisateur
    private async Task<TotpStatusDto?> GetTotpStatusAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.GetAsync("/api/auth/2fa/status", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TotpStatusDto>(HttpJsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private record TotpStatusDto(bool TotpEnabled);
}
