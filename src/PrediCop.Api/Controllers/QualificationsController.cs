using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class QualificationsController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    /// <summary>
    /// GET /api/qualifications?agentId=&type=&expiredOnly=
    /// Liste des habilitations du tenant, filtrables.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<QualificationResponse>>> GetAll(
        [FromQuery] Guid? agentId,
        [FromQuery] QualificationType? type,
        [FromQuery] bool? expiredOnly,
        CancellationToken ct)
    {
        var query = db.AgentQualifications
            .Include(q => q.Agent)
            .Where(q => q.TenantId == TenantId);

        if (agentId.HasValue)
            query = query.Where(q => q.AgentId == agentId.Value);

        if (type.HasValue)
            query = query.Where(q => q.Type == type.Value);

        var qualifications = await query
            .OrderBy(q => q.Agent.LastName)
            .ThenBy(q => q.ExpiresAt)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        if (expiredOnly == true)
            qualifications = qualifications.Where(q => q.ExpiresAt < now).ToList();

        return Ok(qualifications.Select(q => MapToResponse(q)).ToList());
    }

    /// <summary>
    /// GET /api/qualifications/expiring
    /// Habilitations qui expirent dans moins de 30 jours.
    /// </summary>
    [HttpGet("expiring")]
    public async Task<ActionResult<List<QualificationResponse>>> GetExpiring(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddDays(30);

        var qualifications = await db.AgentQualifications
            .Include(q => q.Agent)
            .Where(q => q.TenantId == TenantId
                     && q.ExpiresAt >= now
                     && q.ExpiresAt < threshold)
            .OrderBy(q => q.ExpiresAt)
            .ToListAsync(ct);

        return Ok(qualifications.Select(q => MapToResponse(q)).ToList());
    }

    /// <summary>
    /// POST /api/qualifications
    /// Créer une habilitation.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<QualificationResponse>> Create(
        [FromBody] CreateQualificationRequest request,
        CancellationToken ct)
    {
        // Vérifier que l'agent appartient au tenant
        var agentExists = await db.Users
            .AnyAsync(u => u.Id == request.AgentId && u.TenantId == TenantId && !u.IsDeleted, ct);
        if (!agentExists)
            return Problem(title: "Agent introuvable", statusCode: 404);

        var qualification = new AgentQualification
        {
            TenantId = TenantId,
            AgentId = request.AgentId,
            Type = request.Type,
            Reference = request.Reference,
            IssuingAuthority = request.IssuingAuthority,
            IssuedAt = request.IssuedAt,
            ExpiresAt = request.ExpiresAt,
            Notes = request.Notes
        };

        db.AgentQualifications.Add(qualification);
        await db.SaveChangesAsync(ct);

        await db.Entry(qualification).Reference(q => q.Agent).LoadAsync(ct);

        return CreatedAtAction(nameof(GetAll), new { }, MapToResponse(qualification));
    }

    /// <summary>
    /// PUT /api/qualifications/{id}
    /// Modifier une habilitation.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QualificationResponse>> Update(
        Guid id,
        [FromBody] UpdateQualificationRequest request,
        CancellationToken ct)
    {
        var qualification = await db.AgentQualifications
            .Include(q => q.Agent)
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == TenantId, ct);

        if (qualification is null)
            return Problem(title: "Habilitation introuvable", statusCode: 404);

        qualification.Type = request.Type;
        qualification.Reference = request.Reference;
        qualification.IssuingAuthority = request.IssuingAuthority;
        qualification.IssuedAt = request.IssuedAt;
        qualification.ExpiresAt = request.ExpiresAt;
        qualification.Notes = request.Notes;

        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(qualification));
    }

    /// <summary>
    /// DELETE /api/qualifications/{id}
    /// Soft delete d'une habilitation.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var qualification = await db.AgentQualifications
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == TenantId, ct);

        if (qualification is null)
            return Problem(title: "Habilitation introuvable", statusCode: 404);

        qualification.IsDeleted = true;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static QualificationResponse MapToResponse(AgentQualification q) => new(
        q.Id,
        q.AgentId,
        q.Agent.FullName,
        q.Agent.BadgeNumber,
        q.Type,
        q.Reference,
        q.IssuingAuthority,
        q.IssuedAt,
        q.ExpiresAt,
        q.Notes,
        q.IsExpired,
        q.ExpiresWithin30Days
    );
}
