using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.Subscription;

[Authorize]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public string SubscriptionStatus { get; set; } = "—";
    public string SubscriptionPlan { get; set; } = "—";
    public string BillingPeriod { get; set; } = "—";
    public int VehicleCount { get; set; }
    public int VehicleLimit { get; set; }
    public int UserCount { get; set; }
    public int UserLimit { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool HasStripeSubscription { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            var stats = await client.GetFromJsonAsync<SubscriptionStatsDto>("/api/tenants/my/subscription-stats", JsonOpts, ct);
            if (stats is not null)
            {
                SubscriptionStatus = stats.SubscriptionStatus;
                SubscriptionPlan = stats.SubscriptionPlan;
                BillingPeriod = stats.SubscriptionPeriod == "Yearly" ? "Annuelle" : "Mensuelle";
                VehicleCount = stats.VehicleCount;
                VehicleLimit = stats.VehicleLimit;
                UserCount = stats.UserCount;
                UserLimit = stats.UserLimit;
                CurrentPeriodEnd = stats.CurrentPeriodEnd;
                HasStripeSubscription = stats.HasStripeSubscription;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load subscription stats");
            var jwt = HttpContext.Session.GetString("JwtToken");
            if (jwt is not null)
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                if (handler.CanReadToken(jwt))
                {
                    var token = handler.ReadJwtToken(jwt);
                    SubscriptionStatus = token.Claims.FirstOrDefault(c => c.Type == "subscriptionStatus")?.Value ?? "—";
                    SubscriptionPlan = token.Claims.FirstOrDefault(c => c.Type == "subscriptionPlan")?.Value ?? "—";
                    var vl = token.Claims.FirstOrDefault(c => c.Type == "vehicleLimit")?.Value;
                    var ul = token.Claims.FirstOrDefault(c => c.Type == "userLimit")?.Value;
                    if (int.TryParse(vl, out var vlInt)) VehicleLimit = vlInt;
                    if (int.TryParse(ul, out var ulInt)) UserLimit = ulInt;
                }
            }
        }
    }

    public async Task<IActionResult> OnPostPortalAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync("/api/stripe/portal-session",
                new { returnUrl = "https://localhost:7218/Admin/Subscription" }, JsonOpts, ct);

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Impossible d'accéder au portail de facturation.";
                return RedirectToPage();
            }

            var result = await response.Content.ReadFromJsonAsync<PortalResult>(JsonOpts, ct);
            if (result?.Url is null)
            {
                TempData["ErrorMessage"] = "Réponse inattendue du serveur.";
                return RedirectToPage();
            }

            return Redirect(result.Url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating portal session");
            TempData["ErrorMessage"] = "Erreur lors de la connexion au portail Stripe.";
            return RedirectToPage();
        }
    }

    private record SubscriptionStatsDto(
        string SubscriptionStatus, string SubscriptionPlan, string SubscriptionPeriod,
        int VehicleCount, int VehicleLimit, int UserCount, int UserLimit,
        DateTime? CurrentPeriodEnd, bool HasStripeSubscription);

    private record PortalResult(string? Url);
}
