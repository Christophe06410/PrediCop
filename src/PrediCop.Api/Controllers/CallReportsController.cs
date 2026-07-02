using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/call-reports")]
[Authorize]
public class CallReportsController(AppDbContext db, ILogger<CallReportsController> logger) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);
    private Guid UserId => Guid.Parse(User.FindFirst("userId")!.Value);

    [HttpGet]
    public async Task<ActionResult<List<CallReportResponse>>> GetByCall(
        [FromQuery] Guid callId,
        CancellationToken ct)
    {
        var reports = await db.CallReports
            .Include(r => r.Author)
            .Include(r => r.Call)
            .Where(r => r.CallId == callId && r.TenantId == TenantId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(reports.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CallReportResponse>> GetById(Guid id, CancellationToken ct)
    {
        var report = await db.CallReports
            .Include(r => r.Author)
            .Include(r => r.Call)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);

        if (report is null)
            return Problem(title: "Rapport non trouvé", statusCode: 404);

        return Ok(Map(report));
    }

    [HttpPost]
    public async Task<ActionResult<CallReportResponse>> Create(
        [FromBody] CreateCallReportRequest request,
        CancellationToken ct)
    {
        var call = await db.Calls
            .FirstOrDefaultAsync(c => c.Id == request.CallId && c.TenantId == TenantId, ct);

        if (call is null)
            return Problem(title: "Main courante non trouvée", statusCode: 404);

        var report = new CallReport
        {
            TenantId = TenantId,
            CallId = request.CallId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            AuthorId = UserId,
            Recipients = (ReportRecipient)request.Recipients,
            IsDraft = request.IsDraft,
            FinalizedAt = request.IsDraft ? null : DateTime.UtcNow,
        };

        db.CallReports.Add(report);
        await db.SaveChangesAsync(ct);

        await db.Entry(report).Reference(r => r.Author).LoadAsync(ct);
        await db.Entry(report).Reference(r => r.Call).LoadAsync(ct);

        logger.LogInformation("[CallReports] Rapport {Type} créé ({Id}) sur main courante {CallRef} par {UserId}",
            report.Type, report.Id, call.Reference, UserId);

        return CreatedAtAction(nameof(GetById), new { id = report.Id }, Map(report));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CallReportResponse>> Update(
        Guid id,
        [FromBody] UpdateCallReportRequest request,
        CancellationToken ct)
    {
        var report = await db.CallReports
            .Include(r => r.Author)
            .Include(r => r.Call)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);

        if (report is null)
            return Problem(title: "Rapport non trouvé", statusCode: 404);

        if (request.Type.HasValue) report.Type = request.Type.Value;
        if (request.Title is not null) report.Title = request.Title;
        if (request.Body is not null) report.Body = request.Body;
        if (request.Recipients.HasValue) report.Recipients = (ReportRecipient)request.Recipients.Value;
        if (request.IsDraft.HasValue)
        {
            var wasFinalized = !report.IsDraft;
            report.IsDraft = request.IsDraft.Value;
            if (!request.IsDraft.Value && !wasFinalized)
                report.FinalizedAt = DateTime.UtcNow;
            else if (request.IsDraft.Value)
                report.FinalizedAt = null;
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(report));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var report = await db.CallReports
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);

        if (report is null)
            return Problem(title: "Rapport non trouvé", statusCode: 404);

        report.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CallReportResponse Map(CallReport r) => new()
    {
        Id = r.Id,
        CallId = r.CallId,
        CallReference = r.Call?.Reference ?? string.Empty,
        Type = r.Type.ToString(),
        TypeLabel = r.Type switch
        {
            ReportType.Information     => "Rapport d'information",
            ReportType.Intervention    => "Rapport d'intervention",
            ReportType.CustodyTransfer => "Rapport de mise à disposition",
            ReportType.FormalRecord    => "Procès-verbal",
            _                          => r.Type.ToString()
        },
        Title = r.Title,
        Body = r.Body,
        AuthorId = r.AuthorId,
        AuthorName = r.Author?.FullName ?? string.Empty,
        Recipients = (int)r.Recipients,
        RecipientLabels = GetRecipientLabels(r.Recipients),
        IsDraft = r.IsDraft,
        FinalizedAt = r.FinalizedAt,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static string[] GetRecipientLabels(ReportRecipient recipients)
    {
        var labels = new List<string>();
        if (recipients.HasFlag(ReportRecipient.Mayor))      labels.Add("Maire");
        if (recipients.HasFlag(ReportRecipient.Prosecutor)) labels.Add("Procureur");
        if (recipients.HasFlag(ReportRecipient.Prefecture)) labels.Add("Préfecture");
        if (recipients.HasFlag(ReportRecipient.Hierarchy))  labels.Add("Hiérarchie");
        if (recipients.HasFlag(ReportRecipient.Department)) labels.Add("Service");
        return [.. labels];
    }
}
