using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeoZonesController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpGet]
    public async Task<ActionResult<List<GeoZoneResponse>>> GetAll(CancellationToken ct)
    {
        var zones = await db.GeoZones
            .Include(z => z.Vertices.OrderBy(v => v.Order))
            .Where(z => z.TenantId == TenantId)
            .OrderBy(z => z.Name)
            .ToListAsync(ct);

        return Ok(zones.Select(MapToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GeoZoneResponse>> GetOne(Guid id, CancellationToken ct)
    {
        var zone = await db.GeoZones
            .Include(z => z.Vertices.OrderBy(v => v.Order))
            .FirstOrDefaultAsync(z => z.Id == id && z.TenantId == TenantId, ct);

        if (zone is null) return Problem(title: "Zone non trouvée", statusCode: 404);
        return Ok(MapToResponse(zone));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<GeoZoneResponse>> Create(
        [FromBody] CreateGeoZoneRequest request, CancellationToken ct)
    {
        var zone = new GeoZone
        {
            TenantId = TenantId,
            Name = request.Name,
            Description = request.Description,
            Color = request.Color
        };

        for (int i = 0; i < request.Vertices.Count; i++)
        {
            var v = request.Vertices[i];
            zone.Vertices.Add(new GeoZoneVertex { Order = i, Latitude = v.Latitude, Longitude = v.Longitude });
        }

        db.GeoZones.Add(zone);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetOne), new { id = zone.Id }, MapToResponse(zone));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<GeoZoneResponse>> Update(
        Guid id, [FromBody] UpdateGeoZoneRequest request, CancellationToken ct)
    {
        var zone = await db.GeoZones
            .Include(z => z.Vertices)
            .FirstOrDefaultAsync(z => z.Id == id && z.TenantId == TenantId, ct);

        if (zone is null) return Problem(title: "Zone non trouvée", statusCode: 404);

        if (request.Name is not null) zone.Name = request.Name;
        if (request.Description is not null) zone.Description = request.Description;
        if (request.Color is not null) zone.Color = request.Color;
        if (request.IsActive.HasValue) zone.IsActive = request.IsActive.Value;

        if (request.Vertices is not null)
        {
            db.GeoZoneVertices.RemoveRange(zone.Vertices);
            zone.Vertices.Clear();
            for (int i = 0; i < request.Vertices.Count; i++)
            {
                var v = request.Vertices[i];
                zone.Vertices.Add(new GeoZoneVertex
                {
                    GeoZoneId = zone.Id,
                    Order = i,
                    Latitude = v.Latitude,
                    Longitude = v.Longitude
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(zone));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var zone = await db.GeoZones
            .FirstOrDefaultAsync(z => z.Id == id && z.TenantId == TenantId, ct);

        if (zone is null) return Problem(title: "Zone non trouvée", statusCode: 404);

        zone.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static GeoZoneResponse MapToResponse(GeoZone z) => new()
    {
        Id = z.Id,
        Name = z.Name,
        Description = z.Description,
        Color = z.Color,
        IsActive = z.IsActive,
        Vertices = z.Vertices
            .OrderBy(v => v.Order)
            .Select(v => new GeoZoneVertexDto { Order = v.Order, Latitude = v.Latitude, Longitude = v.Longitude })
            .ToList()
    };
}
