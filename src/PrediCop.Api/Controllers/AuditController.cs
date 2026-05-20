using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    /// <summary>
    /// GET /api/audit?entityName=&amp;entityId=&amp;action=&amp;from=&amp;to=&amp;page=1&amp;size=50
    /// Retourne la liste paginée des logs d'audit du tenant courant, triée par Timestamp desc.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditLogPagedResponse>> GetAuditLogs(
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] string? userName,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 200) size = 50;

        var query = db.AuditLogs
            .Where(a => a.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(a => a.EntityName == entityName);

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(a => a.UserName.Contains(userName));

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(a => new AuditLogResponse
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                UserName = a.UserName,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues
            })
            .ToListAsync(ct);

        return Ok(new AuditLogPagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            Size = size
        });
    }
}
