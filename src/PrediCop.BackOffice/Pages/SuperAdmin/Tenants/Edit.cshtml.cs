using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.SuperAdmin.Tenants;

[Authorize(Roles = "SuperAdmin")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    [BindProperty] public CreateTenantRequest CreateRequest { get; set; } = new();
    [BindProperty] public UpdateTenantRequest UpdateRequest { get; set; } = new();
    [BindProperty] public Guid? TenantId { get; set; }

    public TenantAdminDto? Tenant { get; set; }
    public bool IsEdit => TenantId.HasValue;

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        TenantId = id;
        if (id.HasValue)
        {
            try
            {
                var client = httpClientFactory.CreateClient("PrediCopApi");
                Tenant = await client.GetFromJsonAsync<TenantAdminDto>($"/api/superadmin/tenants/{id}");
                if (Tenant is not null)
                {
                    UpdateRequest.Name = Tenant.Name;
                    UpdateRequest.IsActive = Tenant.IsActive;
                    UpdateRequest.SubscriptionPlan = Tenant.SubscriptionPlan;
                    UpdateRequest.SubscriptionStatus = Tenant.SubscriptionStatus;
                    UpdateRequest.VehicleLimit = Tenant.VehicleLimit;
                    UpdateRequest.UserLimit = Tenant.UserLimit;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible de charger le tenant {Id}.", id);
                TempData["ErrorMessage"] = "Tenant introuvable.";
                return RedirectToPage("Index");
            }
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/superadmin/tenants", CreateRequest);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Ville \"{CreateRequest.Name}\" créée avec succès.";
                return RedirectToPage("Index");
            }
            var error = await response.Content.ReadAsStringAsync();
            TempData["ErrorMessage"] = $"Erreur : {response.StatusCode}. {error}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la création du tenant.");
            TempData["ErrorMessage"] = "Erreur lors de la création.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id)
    {
        TenantId = id;
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PutAsJsonAsync($"/api/superadmin/tenants/{id}", UpdateRequest);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Tenant mis à jour.";
                return RedirectToPage("Index");
            }
            TempData["ErrorMessage"] = $"Erreur : {response.StatusCode}.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la modification du tenant {Id}.", id);
            TempData["ErrorMessage"] = "Erreur lors de la modification.";
        }
        return Page();
    }
}
