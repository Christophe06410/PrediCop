using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrediCop.Api.Settings;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeController(
    AppDbContext db,
    IOptions<StripeSettings> stripeOptions,
    ILogger<StripeController> logger) : ControllerBase
{
    private readonly StripeSettings _stripe = stripeOptions.Value;

    [HttpPost("checkout-session")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateSubscriptionRequest req, CancellationToken ct)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == req.Slug, ct))
            return Problem(title: "Ce slug est déjà utilisé", statusCode: 409);

        if (await db.Users.AnyAsync(u => u.Email == req.AdminEmail && !u.IsDeleted, ct))
            return Problem(title: "Cette adresse email est déjà utilisée", statusCode: 409);

        var (vehicleLimit, userLimit) = req.Plan switch
        {
            SubscriptionPlan.Standard => (20, 9999),
            SubscriptionPlan.Premium => (9999, 9999),
            _ => (5, 5)
        };

        var priceId = (req.Plan, req.Period) switch
        {
            (SubscriptionPlan.Essential, SubscriptionPeriod.Yearly) => _stripe.EssentialYearlyPriceId,
            (SubscriptionPlan.Standard, SubscriptionPeriod.Monthly) => _stripe.StandardMonthlyPriceId,
            (SubscriptionPlan.Standard, SubscriptionPeriod.Yearly) => _stripe.StandardYearlyPriceId,
            _ => _stripe.EssentialMonthlyPriceId
        };

        var selectedModules = ParseModules(req.Modules);

        var tenant = new Tenant
        {
            Name = req.TenantName,
            Slug = req.Slug,
            IsActive = true,
            SubscriptionStatus = SubscriptionStatus.None,
            SubscriptionPlan = req.Plan,
            SubscriptionPeriod = req.Period,
            VehicleLimit = vehicleLimit,
            UserLimit = userLimit,
            ModuleRhEnabled            = selectedModules.Contains("rh"),
            ModuleVerbalisationEnabled = selectedModules.Contains("verbalisation"),
            ModuleFourriereEnabled     = selectedModules.Contains("fourriere"),
            ModuleFleetEnabled         = selectedModules.Contains("fleet"),
            ModuleLogisticsEnabled     = selectedModules.Contains("logistics"),
            GeofencingEnabled          = selectedModules.Contains("geofencing"),
        };
        db.Tenants.Add(tenant);

        var adminUser = new User
        {
            TenantId = tenant.Id,
            FirstName = "Admin",
            LastName = req.TenantName,
            Email = req.AdminEmail,
            BadgeNumber = "ADMIN",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync(ct);

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            CustomerEmail = req.AdminEmail,
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenant.Id.ToString(),
                ["modules"]  = req.Modules ?? ""
            },
            SuccessUrl = _stripe.SuccessUrl,
            CancelUrl = _stripe.CancelUrl
        };

        Session session;
        try
        {
            session = await new SessionService().CreateAsync(sessionOptions, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe checkout session creation failed for tenant {Slug}", req.Slug);
            db.Users.Remove(adminUser);
            db.Tenants.Remove(tenant);
            await db.SaveChangesAsync(ct);
            return Problem(title: "Erreur Stripe: " + ex.Message, statusCode: 502);
        }

        tenant.StripeCheckoutSessionId = session.Id;
        await db.SaveChangesAsync(ct);

        return Ok(new { Url = session.Url });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            json = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].ToString();
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, _stripe.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook validation failed: {Message}", ex.Message);
            return BadRequest();
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            {
                if (stripeEvent.Data.Object is not Session session) break;
                if (session.Metadata?.TryGetValue("tenantId", out var tenantIdStr) != true) break;
                if (!Guid.TryParse(tenantIdStr, out var tenantId)) break;

                var tenant = await db.Tenants.FindAsync([tenantId], ct);
                if (tenant is null) break;

                tenant.StripeCustomerId = session.CustomerId;
                tenant.StripeSubscriptionId = session.SubscriptionId;
                tenant.SubscriptionStatus = SubscriptionStatus.Active;

                if (session.SubscriptionId is not null)
                {
                    try
                    {
                        var sub = await new SubscriptionService().GetAsync(session.SubscriptionId, cancellationToken: ct);
                        tenant.CurrentPeriodEnd = sub.CurrentPeriodEnd;
                    }
                    catch (StripeException ex)
                    {
                        logger.LogWarning(ex, "Could not fetch subscription details for {SubscriptionId}", session.SubscriptionId);
                    }
                }
                await db.SaveChangesAsync(ct);
                break;
            }

            case "customer.subscription.updated":
            {
                if (stripeEvent.Data.Object is not Stripe.Subscription sub) break;
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.StripeSubscriptionId == sub.Id, ct);
                if (tenant is null) break;

                tenant.SubscriptionStatus = sub.Status switch
                {
                    "active" => SubscriptionStatus.Active,
                    "trialing" => SubscriptionStatus.Trialing,
                    "past_due" => SubscriptionStatus.PastDue,
                    "canceled" or "cancelled" => SubscriptionStatus.Cancelled,
                    _ => tenant.SubscriptionStatus
                };
                tenant.CurrentPeriodEnd = sub.CurrentPeriodEnd;
                await db.SaveChangesAsync(ct);
                break;
            }

            case "customer.subscription.deleted":
            {
                if (stripeEvent.Data.Object is not Stripe.Subscription sub) break;
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.StripeSubscriptionId == sub.Id, ct);
                if (tenant is null) break;

                tenant.SubscriptionStatus = SubscriptionStatus.Cancelled;
                await db.SaveChangesAsync(ct);
                break;
            }

            case "invoice.payment_failed":
            {
                if (stripeEvent.Data.Object is not Invoice invoice) break;
                if (invoice.SubscriptionId is null) break;

                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.StripeSubscriptionId == invoice.SubscriptionId, ct);
                if (tenant is null) break;

                tenant.SubscriptionStatus = SubscriptionStatus.PastDue;
                await db.SaveChangesAsync(ct);
                break;
            }
        }

        return Ok();
    }

    [HttpPost("portal-session")]
    [Authorize]
    public async Task<IActionResult> CreatePortalSession(
        [FromBody] PortalSessionRequest req, CancellationToken ct)
    {
        var tenantIdStr = User.FindFirst("tenantId")?.Value;
        if (tenantIdStr is null || !Guid.TryParse(tenantIdStr, out var tenantId))
            return Unauthorized();

        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        if (tenant?.StripeCustomerId is null)
            return Problem(title: "Aucun client Stripe associé à ce compte", statusCode: 404);

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            ReturnUrl = req.ReturnUrl ?? "https://localhost:7218/Admin/Subscription"
        };
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(options, cancellationToken: ct);
        return Ok(new { Url = session.Url });
    }

    private static HashSet<string> ParseModules(string? modules)
    {
        if (string.IsNullOrWhiteSpace(modules)) return [];
        var valid = new HashSet<string> { "rh", "verbalisation", "fourriere", "fleet", "logistics", "geofencing" };
        return modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(m => m.ToLowerInvariant())
                      .Where(valid.Contains)
                      .ToHashSet();
    }
}

public record CreateSubscriptionRequest(
    [Required] string TenantName,
    [Required][RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Le slug ne peut contenir que des lettres minuscules, chiffres et tirets")] string Slug,
    [Required][EmailAddress] string AdminEmail,
    [Required][MinLength(8)] string AdminPassword,
    SubscriptionPlan Plan = SubscriptionPlan.Essential,
    SubscriptionPeriod Period = SubscriptionPeriod.Monthly,
    string? Modules = null);

public record PortalSessionRequest(string? ReturnUrl);

