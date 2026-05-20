using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrediCop.Core.DTOs;
using PrediCop.Core.Interfaces;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/shift-reports")]
[Authorize]
public class ShiftReportsController(IShiftReportService shiftReportService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    /// <summary>Liste paginée des rapports de vacation, filtrable par véhicule et plage de dates.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<List<ShiftReportResponse>>> GetList(
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var (items, total) = await shiftReportService.GetListAsync(TenantId, vehicleId, dateFrom, dateTo, page, pageSize, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    /// <summary>Génère et sauvegarde un rapport de vacation.</summary>
    [HttpPost]
    [Authorize(Roles = "Officer,Manager")]
    public async Task<ActionResult<ShiftReportResponse>> Create(
        [FromBody] CreateShiftReportRequest request,
        CancellationToken ct)
    {
        try
        {
            var report = await shiftReportService.GenerateAsync(request, TenantId, ct);
            return CreatedAtAction(nameof(GetById), new { id = report.Id }, report);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: ex.Message, statusCode: 400);
        }
    }

    /// <summary>Détail d'un rapport de vacation.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftReportResponse>> GetById(Guid id, CancellationToken ct)
    {
        var report = await shiftReportService.GetAsync(id, TenantId, ct);
        if (report is null)
            return Problem(title: "Rapport de vacation non trouvé", statusCode: 404);

        return Ok(report);
    }

    /// <summary>Signe électroniquement un rapport de vacation.</summary>
    [HttpPost("{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid id, CancellationToken ct)
    {
        try
        {
            await shiftReportService.SignAsync(id, TenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: ex.Message, statusCode: 400);
        }
    }
}
