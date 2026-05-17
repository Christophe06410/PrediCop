using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrackingController(AppDbContext db, IEmailService emailService) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;
    private Guid UserId => Guid.Parse(User.FindFirst("userId")!.Value);
    private bool IsManagerOrAdmin => User.IsInRole("Manager") || User.IsInRole("Admin");

    // GET /api/tracking?missionId=...
    [HttpGet]
    public async Task<ActionResult<List<TrackingDocumentResponse>>> GetAll(
        [FromQuery] Guid? missionId,
        [FromQuery] DocumentType? type,
        [FromQuery] DocumentStatus? status,
        CancellationToken ct)
    {
        var query = db.TrackingDocuments
            .Include(d => d.CreatedBy)
            .Include(d => d.Mission)
            .Where(d => d.TenantId == TenantId);

        if (missionId.HasValue) query = query.Where(d => d.MissionId == missionId.Value);
        if (type.HasValue) query = query.Where(d => d.Type == type.Value);
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);

        var docs = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        return Ok(docs.Select(MapToResponse).ToList());
    }

    // GET /api/tracking/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrackingDocumentResponse>> GetById(Guid id, CancellationToken ct)
    {
        var doc = await db.TrackingDocuments
            .Include(d => d.CreatedBy)
            .Include(d => d.Mission)
            .Include(d => d.Entries.OrderBy(e => e.OccurredAt))
                .ThenInclude(e => e.CreatedBy)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);

        return doc is null ? NotFound() : Ok(MapToResponse(doc));
    }

    // POST /api/tracking
    [HttpPost]
    public async Task<ActionResult<TrackingDocumentResponse>> Create(
        [FromBody] CreateTrackingDocumentRequest req, CancellationToken ct)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(
            m => m.Id == req.MissionId && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission introuvable", statusCode: 404);

        if (mission.Status == MissionStatus.Pending || mission.Status == MissionStatus.Proposed)
            return Problem(title: "La mission doit être au moins en cours pour créer un document de suivi", statusCode: 400);

        var prefix = req.Type == DocumentType.Plainte ? "PL" : "MC";
        var reference = $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var doc = new TrackingDocument
        {
            TenantId = TenantId,
            Reference = reference,
            Type = req.Type,
            Status = DocumentStatus.Brouillon,
            Title = req.Title,
            MissionId = req.MissionId,
            CreatedByUserId = UserId
        };

        db.TrackingDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        await db.Entry(doc).Reference(d => d.CreatedBy).LoadAsync(ct);
        await db.Entry(doc).Reference(d => d.Mission).LoadAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, MapToResponse(doc));
    }

    // PUT /api/tracking/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrackingDocumentResponse>> Update(
        Guid id, [FromBody] UpdateTrackingDocumentRequest req, CancellationToken ct)
    {
        var doc = await db.TrackingDocuments
            .Include(d => d.CreatedBy).Include(d => d.Mission)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);

        if (doc is null) return NotFound();
        if (doc.Status == DocumentStatus.Archive)
            return Problem(title: "Un document archivé ne peut plus être modifié", statusCode: 400);

        if (req.Title is not null) doc.Title = req.Title;
        if (req.Status.HasValue) doc.Status = req.Status.Value;

        await db.SaveChangesAsync(ct);

        // Notifications email lors de transitions de statut importantes
        if (req.Status.HasValue)
        {
            try
            {
                string? emailSubject = req.Status.Value switch
                {
                    DocumentStatus.EnvoyeParquet => $"[PrediCop] Document {doc.Reference} transmis au Parquet",
                    DocumentStatus.EnvoyeJuge    => $"[PrediCop] Document {doc.Reference} transmis au Juge",
                    _                            => null
                };

                if (emailSubject is not null)
                {
                    var destinataire = req.Status.Value == DocumentStatus.EnvoyeParquet ? "au Parquet" : "au Juge";
                    var htmlBody = $"""
                        <html><body style="font-family:Arial,sans-serif;color:#1a2035;max-width:600px;margin:auto;">
                        <h2 style="color:#2563eb;border-bottom:2px solid #2563eb;padding-bottom:8px;">
                            PrediCop — Notification document
                        </h2>
                        <p>Le document suivant vient d'être transmis <strong>{destinataire}</strong>.</p>
                        <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                            <tr style="background:#f1f5f9;">
                                <td style="padding:8px 12px;font-weight:bold;width:40%;">Référence</td>
                                <td style="padding:8px 12px;">{doc.Reference}</td>
                            </tr>
                            <tr>
                                <td style="padding:8px 12px;font-weight:bold;">Titre</td>
                                <td style="padding:8px 12px;">{System.Net.WebUtility.HtmlEncode(doc.Title)}</td>
                            </tr>
                            <tr style="background:#f1f5f9;">
                                <td style="padding:8px 12px;font-weight:bold;">Mission</td>
                                <td style="padding:8px 12px;">{doc.Mission?.Reference ?? "N/A"}</td>
                            </tr>
                            <tr>
                                <td style="padding:8px 12px;font-weight:bold;">Finalisé par</td>
                                <td style="padding:8px 12px;">{System.Net.WebUtility.HtmlEncode(doc.CreatedBy?.FullName ?? "N/A")}</td>
                            </tr>
                            <tr style="background:#f1f5f9;">
                                <td style="padding:8px 12px;font-weight:bold;">Date</td>
                                <td style="padding:8px 12px;">{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</td>
                            </tr>
                        </table>
                        <hr style="border:none;border-top:1px solid #e2e8f0;margin:24px 0;"/>
                        <small style="color:#64748b;">PrediCop — Police Municipale | Ce message est généré automatiquement.</small>
                        </body></html>
                        """;

                    await emailService.SendToManagersAsync(TenantId, emailSubject, htmlBody, ct);
                }
            }
            catch (Exception)
            {
                // L'envoi email ne doit jamais faire échouer l'action principale
            }
        }

        return Ok(MapToResponse(doc));
    }

    // DELETE /api/tracking/{id}  — Manager/Admin only
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await db.TrackingDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);

        if (doc is null) return NotFound();

        doc.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // POST /api/tracking/{id}/entries
    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<TrackingDocumentResponse>> AddEntry(
        Guid id, [FromBody] AddTrackingEntryRequest req, CancellationToken ct)
    {
        var doc = await db.TrackingDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);

        if (doc is null) return NotFound();
        if (doc.Status == DocumentStatus.Archive)
            return Problem(title: "Impossible d'ajouter une entrée à un document archivé", statusCode: 400);

        var entry = new TrackingEntry
        {
            TenantId = TenantId,
            DocumentId = id,
            Type = req.Type,
            Content = req.Content,
            OccurredAt = req.OccurredAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(req.OccurredAt, DateTimeKind.Utc)
                : req.OccurredAt.ToUniversalTime(),
            CreatedByUserId = UserId
        };

        db.TrackingEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return await GetById(id, ct);
    }

    // PUT /api/tracking/{id}/entries/{entryId}
    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<TrackingDocumentResponse>> UpdateEntry(
        Guid id, Guid entryId, [FromBody] UpdateTrackingEntryRequest req, CancellationToken ct)
    {
        var entry = await db.TrackingEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.DocumentId == id && e.TenantId == TenantId, ct);

        if (entry is null) return NotFound();

        if (req.Content is not null) entry.Content = req.Content;
        if (req.Type.HasValue) entry.Type = req.Type.Value;
        if (req.OccurredAt.HasValue)
        {
            var dt = req.OccurredAt.Value;
            entry.OccurredAt = dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime();
        }

        await db.SaveChangesAsync(ct);
        return await GetById(id, ct);
    }

    // DELETE /api/tracking/{id}/entries/{entryId}  — Manager/Admin only
    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> DeleteEntry(Guid id, Guid entryId, CancellationToken ct)
    {
        var entry = await db.TrackingEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.DocumentId == id && e.TenantId == TenantId, ct);

        if (entry is null) return NotFound();

        entry.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static TrackingDocumentResponse MapToResponse(TrackingDocument d) => new()
    {
        Id = d.Id,
        Reference = d.Reference,
        Type = d.Type,
        Status = d.Status,
        Title = d.Title,
        MissionId = d.MissionId,
        MissionReference = d.Mission?.Reference ?? string.Empty,
        CreatedByUserId = d.CreatedByUserId,
        CreatedByName = d.CreatedBy?.FullName ?? string.Empty,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        Entries = d.Entries.Select(e => new TrackingEntryResponse
        {
            Id = e.Id,
            DocumentId = e.DocumentId,
            OccurredAt = e.OccurredAt,
            Type = e.Type,
            Content = e.Content,
            CreatedByUserId = e.CreatedByUserId,
            CreatedByName = e.CreatedBy?.FullName ?? string.Empty,
            CreatedAt = e.CreatedAt
        }).ToList()
    };
}
