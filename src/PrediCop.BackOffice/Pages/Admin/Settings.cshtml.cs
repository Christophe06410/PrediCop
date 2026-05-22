using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin;

[Authorize(Roles = "Admin")]
public class SettingsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(IHttpClientFactory httpClientFactory, ILogger<SettingsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool GeofencingEnabled { get; set; }
    public string? DpoEmail { get; set; }
    public TenantFeatureFlagsResponse? Features { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");

            var settingsTask = client.GetFromJsonAsync<TenantSettingsDto>("/api/tenants/settings");
            var featuresTask = client.GetFromJsonAsync<TenantFeatureFlagsResponse>("/api/tenant/features");

            await Task.WhenAll(settingsTask, featuresTask);

            var settings = settingsTask.Result;
            if (settings is not null)
            {
                GeofencingEnabled = settings.GeofencingEnabled;
                DpoEmail = settings.DpoEmail;
            }

            Features = featuresTask.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les paramètres du tenant.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGeofencingAsync(bool Enabled)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PatchAsJsonAsync("/api/tenants/geofencing", new { Enabled });

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = Enabled
                    ? "Le géofencing a été activé."
                    : "Le géofencing a été désactivé.";
            else
                TempData["ErrorMessage"] = "Impossible de modifier le géofencing.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la modification du géofencing.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDpoEmailAsync(string? DpoEmail)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PatchAsJsonAsync("/api/tenants/dpo-email", new { DpoEmail });

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "L'email DPO a été mis à jour.";
            else
                TempData["ErrorMessage"] = "Impossible de mettre à jour l'email DPO.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la modification de l'email DPO.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostModulesAsync(
        bool ModuleRhEnabled,
        bool ModuleFourriereEnabled,
        bool ModuleFleetEnabled,
        bool ModuleLogisticsEnabled,
        bool ModuleVerbalisationEnabled)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var request = new UpdateModuleFlagsRequest(
                ModuleRhEnabled,
                ModuleFourriereEnabled,
                ModuleFleetEnabled,
                ModuleLogisticsEnabled,
                ModuleVerbalisationEnabled);

            var response = await client.PatchAsJsonAsync("/api/tenant/features/modules", request);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Les modules ont été mis à jour.";
            else
                TempData["ErrorMessage"] = "Impossible de mettre à jour les modules.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la mise à jour des modules.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSensitiveFieldsAsync(
        bool AgentBloodTypeEnabled,
        bool AgentEmergencyContactEnabled,
        bool GpsTrackingEnabled,
        bool GeofencingEnabled,
        bool PhotoAttachmentsEnabled,
        int GpsDataRetentionDays,
        int AuditLogRetentionDays)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var request = new UpdateSensitiveFieldFlagsRequest(
                AgentBloodTypeEnabled,
                AgentEmergencyContactEnabled,
                GpsTrackingEnabled,
                GeofencingEnabled,
                PhotoAttachmentsEnabled,
                GpsDataRetentionDays,
                AuditLogRetentionDays);

            var response = await client.PatchAsJsonAsync("/api/tenant/features/sensitive-fields", request);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Les paramètres de champs sensibles ont été mis à jour.";
            else
                TempData["ErrorMessage"] = "Impossible de mettre à jour les champs sensibles.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la mise à jour des champs sensibles.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage();
    }

    // DTO local pour désérialiser la réponse API tenants/settings
    private class TenantSettingsDto
    {
        public bool GeofencingEnabled { get; set; }
        public string? DpoEmail { get; set; }
    }
}
