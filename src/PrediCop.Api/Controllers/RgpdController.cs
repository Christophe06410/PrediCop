using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RgpdController(AppDbContext db, IEmailService emailService, IConfiguration configuration) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    /// <summary>
    /// POST /api/rgpd/requests [AllowAnonymous]
    /// Soumettre une demande RGPD depuis la page publique.
    /// Le body inclut TenantSlug pour résoudre le tenant (utilisateur non authentifié).
    /// </summary>
    [HttpPost("requests")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitRequest(
        [FromBody] SubmitRgpdRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Problem(title: "Le slug du tenant est requis", statusCode: 400);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == request.TenantSlug && t.IsActive, ct);
        if (tenant is null)
            return Problem(title: "Organisation introuvable", statusCode: 404);

        if (string.IsNullOrWhiteSpace(request.RequesterName))
            return Problem(title: "Le nom est requis", statusCode: 400);

        if (string.IsNullOrWhiteSpace(request.RequesterEmail))
            return Problem(title: "L'email est requis", statusCode: 400);

        if (string.IsNullOrWhiteSpace(request.Description))
            return Problem(title: "La description est requise", statusCode: 400);

        var rgpdRequest = new RgpdRequest
        {
            TenantId = tenant.Id,
            RequestType = request.RequestType,
            RequesterName = request.RequesterName,
            RequesterEmail = request.RequesterEmail,
            Description = request.Description,
            SubmittedAt = DateTime.UtcNow,
            IsProcessed = false
        };

        db.RgpdRequests.Add(rgpdRequest);
        await db.SaveChangesAsync(ct);

        // Envoyer un email au DPO du tenant
        var dpoEmail = tenant.DpoEmail;
        if (string.IsNullOrWhiteSpace(dpoEmail))
            dpoEmail = configuration["EmailSettings:FromAddress"] ?? "noreply@predicop.fr";

        var requestTypeLabel = request.RequestType switch
        {
            Core.Enums.RgpdRequestType.AccesData => "Droit d'accès",
            Core.Enums.RgpdRequestType.Rectification => "Droit de rectification",
            Core.Enums.RgpdRequestType.Suppression => "Droit à l'effacement",
            Core.Enums.RgpdRequestType.Portabilite => "Droit à la portabilité",
            Core.Enums.RgpdRequestType.Opposition => "Droit d'opposition",
            Core.Enums.RgpdRequestType.Limitation => "Droit à la limitation du traitement",
            _ => request.RequestType.ToString()
        };

        var dpoHtml = $"""
            <h2>Nouvelle demande RGPD - {tenant.Name}</h2>
            <p><strong>Type de demande :</strong> {requestTypeLabel}</p>
            <p><strong>Demandeur :</strong> {request.RequesterName}</p>
            <p><strong>Email :</strong> {request.RequesterEmail}</p>
            <p><strong>Description :</strong></p>
            <blockquote>{request.Description}</blockquote>
            <p><strong>Soumise le :</strong> {rgpdRequest.SubmittedAt:dd/MM/yyyy HH:mm} UTC</p>
            <p>Vous pouvez traiter cette demande depuis la console d'administration PrediCop.</p>
            """;

        try
        {
            await emailService.SendAsync(dpoEmail, $"[RGPD] Nouvelle demande - {requestTypeLabel}", dpoHtml, ct);
        }
        catch
        {
            // L'envoi d'email ne doit pas bloquer l'enregistrement de la demande
        }

        // Envoyer une confirmation au demandeur
        var confirmationHtml = $"""
            <h2>Votre demande RGPD a bien été enregistrée</h2>
            <p>Bonjour {request.RequesterName},</p>
            <p>Nous avons bien reçu votre demande de type <strong>{requestTypeLabel}</strong>.</p>
            <p>Vous recevrez une réponse dans un délai maximum d'un mois conformément au RGPD.</p>
            <p>Référence de votre demande : <strong>{rgpdRequest.Id}</strong></p>
            <p>Cordialement,<br/>L'équipe {tenant.Name}</p>
            """;

        try
        {
            await emailService.SendAsync(request.RequesterEmail, "Votre demande RGPD a été enregistrée", confirmationHtml, ct);
        }
        catch
        {
            // L'envoi d'email ne doit pas bloquer l'enregistrement de la demande
        }

        return Ok(new { Id = rgpdRequest.Id, Message = "Votre demande a bien été enregistrée. Un email de confirmation vous a été envoyé." });
    }

    /// <summary>
    /// GET /api/rgpd/requests [Admin]
    /// Liste des demandes RGPD du tenant.
    /// </summary>
    [HttpGet("requests")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<RgpdRequestResponse>>> GetRequests(CancellationToken ct)
    {
        var requests = await db.RgpdRequests
            .Where(r => r.TenantId == TenantId)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new RgpdRequestResponse(
                r.Id,
                r.RequestType,
                r.RequesterName,
                r.RequesterEmail,
                r.Description,
                r.SubmittedAt,
                r.IsProcessed,
                r.ProcessedAt,
                r.AdminNotes
            ))
            .ToListAsync(ct);

        return Ok(requests);
    }

    /// <summary>
    /// PATCH /api/rgpd/requests/{id}/process [Admin]
    /// Marquer une demande comme traitée avec notes admin.
    /// </summary>
    [HttpPatch("requests/{id:guid}/process")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RgpdRequestResponse>> ProcessRequest(
        Guid id,
        [FromBody] ProcessRgpdRequest request,
        CancellationToken ct)
    {
        var rgpdRequest = await db.RgpdRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);

        if (rgpdRequest is null)
            return Problem(title: "Demande introuvable", statusCode: 404);

        rgpdRequest.IsProcessed = true;
        rgpdRequest.ProcessedAt = DateTime.UtcNow;
        rgpdRequest.AdminNotes = request.AdminNotes;

        await db.SaveChangesAsync(ct);

        return Ok(new RgpdRequestResponse(
            rgpdRequest.Id,
            rgpdRequest.RequestType,
            rgpdRequest.RequesterName,
            rgpdRequest.RequesterEmail,
            rgpdRequest.Description,
            rgpdRequest.SubmittedAt,
            rgpdRequest.IsProcessed,
            rgpdRequest.ProcessedAt,
            rgpdRequest.AdminNotes
        ));
    }
}
