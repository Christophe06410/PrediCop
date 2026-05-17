using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController(AppDbContext db, ILogger<SearchController> logger) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private static string Truncate(string? text, int maxLength = 80)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    // GET /api/search?q=&limit=20
    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(new SearchResponse
            {
                Query = q ?? string.Empty,
                TotalCount = 0,
                Results = []
            });
        }

        var query = q.Trim().ToLower();
        var tenantId = TenantId;

        var callsTask = SearchCallsAsync(tenantId, query, ct);
        var missionsTask = SearchMissionsAsync(tenantId, query, ct);
        var documentsTask = SearchDocumentsAsync(tenantId, query, ct);
        var entriesTask = SearchEntriesAsync(tenantId, query, ct);

        await Task.WhenAll(callsTask, missionsTask, documentsTask, entriesTask);

        var results = callsTask.Result
            .Concat(missionsTask.Result)
            .Concat(documentsTask.Result)
            .Concat(entriesTask.Result)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();

        return Ok(new SearchResponse
        {
            Query = q.Trim(),
            TotalCount = results.Count,
            Results = results
        });
    }

    private async Task<List<SearchResultItem>> SearchCallsAsync(Guid tenantId, string query, CancellationToken ct)
    {
        try
        {
            var calls = await db.Calls
                .Where(c => c.TenantId == tenantId &&
                    (c.CallerName.ToLower().Contains(query) ||
                     c.CallerPhone.ToLower().Contains(query) ||
                     c.IncidentAddress.ToLower().Contains(query) ||
                     c.IncidentDescription.ToLower().Contains(query)))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            return calls.Select(c => new SearchResultItem
            {
                Type = "Appel",
                Id = c.Id,
                Reference = c.Reference,
                Title = c.IncidentAddress,
                Subtitle = Truncate(c.IncidentDescription),
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la recherche dans les appels.");
            return [];
        }
    }

    private async Task<List<SearchResultItem>> SearchMissionsAsync(Guid tenantId, string query, CancellationToken ct)
    {
        try
        {
            var missions = await db.Missions
                .Where(m => m.TenantId == tenantId &&
                    (m.Reference.ToLower().Contains(query) ||
                     m.TargetAddress.ToLower().Contains(query) ||
                     m.BriefingText.ToLower().Contains(query)))
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);

            return missions.Select(m => new SearchResultItem
            {
                Type = "Mission",
                Id = m.Id,
                Reference = m.Reference,
                Title = m.TargetAddress,
                Subtitle = Truncate(m.BriefingText),
                Status = m.Status.ToString(),
                CreatedAt = m.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la recherche dans les missions.");
            return [];
        }
    }

    private async Task<List<SearchResultItem>> SearchDocumentsAsync(Guid tenantId, string query, CancellationToken ct)
    {
        try
        {
            var documents = await db.TrackingDocuments
                .Include(d => d.Mission)
                .Where(d => d.TenantId == tenantId &&
                    (d.Reference.ToLower().Contains(query) ||
                     d.Title.ToLower().Contains(query)))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);

            return documents.Select(d => new SearchResultItem
            {
                Type = "Document",
                Id = d.Id,
                Reference = d.Reference,
                Title = d.Title,
                Subtitle = d.Mission?.Reference ?? string.Empty,
                Status = d.Status.ToString(),
                CreatedAt = d.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la recherche dans les documents.");
            return [];
        }
    }

    private async Task<List<SearchResultItem>> SearchEntriesAsync(Guid tenantId, string query, CancellationToken ct)
    {
        try
        {
            var entries = await db.TrackingEntries
                .Where(e => e.TenantId == tenantId &&
                    e.Content.ToLower().Contains(query))
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(ct);

            return entries.Select(e => new SearchResultItem
            {
                Type = "Entrée",
                Id = e.Id,
                Reference = string.Empty,
                Title = Truncate(e.Content),
                Subtitle = string.Empty,
                Status = e.Type.ToString(),
                CreatedAt = e.CreatedAt,
                ParentId = e.DocumentId.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la recherche dans les entrées.");
            return [];
        }
    }
}
