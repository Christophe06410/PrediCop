using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Infrastructure.Data;
using System.Globalization;
using System.Text.Json;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeoZonesController(AppDbContext db, IHttpClientFactory httpClientFactory) : ControllerBase
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

        // Auto-detect streets if the zone already has a valid polygon
        if (zone.Vertices.Count >= 3)
            await SyncStreetsForZoneAsync(zone, ct);

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

        bool verticesChanged = request.Vertices is not null;

        if (verticesChanged)
        {
            db.GeoZoneVertices.RemoveRange(zone.Vertices);
            zone.Vertices.Clear();
            for (int i = 0; i < request.Vertices!.Count; i++)
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

        // Re-sync streets whenever the polygon changes
        if (verticesChanged && zone.Vertices.Count >= 3)
            await SyncStreetsForZoneAsync(zone, ct);

        return Ok(MapToResponse(zone));
    }

    /// <summary>
    /// Manual trigger — still available but Create/Update/Delete call this automatically.
    /// </summary>
    [HttpPost("{id:guid}/detect-streets")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<DetectStreetsResult>> DetectStreets(Guid id, CancellationToken ct)
    {
        var zone = await db.GeoZones
            .Include(z => z.Vertices)
            .FirstOrDefaultAsync(z => z.Id == id && z.TenantId == TenantId, ct);

        if (zone is null) return Problem(title: "Zone non trouvée", statusCode: 404);
        if (zone.Vertices.Count < 3)
            return Problem(title: "La zone doit avoir au minimum 3 sommets.", statusCode: 400);

        var result = await SyncStreetsForZoneAsync(zone, ct);

        if (result.Error is not null)
            return Problem(title: result.Error, statusCode: 502);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var zone = await db.GeoZones
            .FirstOrDefaultAsync(z => z.Id == id && z.TenantId == TenantId, ct);

        if (zone is null) return Problem(title: "Zone non trouvée", statusCode: 404);

        zone.IsDeleted = true;

        // Soft-delete all streets that belong exclusively to this zone
        var streets = await db.Streets
            .Where(s => s.GeoZoneId == id && s.TenantId == TenantId)
            .ToListAsync(ct);

        foreach (var s in streets)
            s.IsDeleted = true;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Core sync logic ────────────────────────────────────────────────────────

    /// <summary>
    /// Queries Overpass for streets inside the zone polygon, then reconciles:
    /// - New streets are added with default risk values.
    /// - Streets already in the DB for this zone keep their existing risk values
    ///   but get updated coordinates.
    /// - Streets previously belonging to this zone that Overpass no longer
    ///   returns are soft-deleted.
    /// </summary>
    private async Task<DetectStreetsResult> SyncStreetsForZoneAsync(GeoZone zone, CancellationToken ct)
    {
        var poly = string.Join(" ", zone.Vertices
            .OrderBy(v => v.Order)
            .SelectMany(v => new[]
            {
                v.Latitude.ToString("F6", CultureInfo.InvariantCulture),
                v.Longitude.ToString("F6", CultureInfo.InvariantCulture)
            }));

        var query = $"[out:json][timeout:30];way[\"highway\"][\"name\"](poly:\"{poly}\");out geom;";

        OverpassResponse? overpass;
        try
        {
            var client = httpClientFactory.CreateClient("Overpass");
            var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
            var resp = await client.PostAsync("https://overpass-api.de/api/interpreter", content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                return new DetectStreetsResult(0, 0, 0,
                    $"Overpass API a retourné HTTP {(int)resp.StatusCode}. Vérifiez la taille de la zone.");
            }
            overpass = await resp.Content.ReadFromJsonAsync<OverpassResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
        }
        catch (TaskCanceledException)
        {
            return new DetectStreetsResult(0, 0, 0,
                "Délai dépassé (30 s). La zone est peut-être trop grande — réduisez-la ou divisez-la en plusieurs zones.");
        }
        catch (Exception ex)
        {
            return new DetectStreetsResult(0, 0, 0, $"Erreur réseau : {ex.Message}");
        }

        if (overpass?.Elements == null) return new DetectStreetsResult(0, 0, 0, null);

        // Collect valid ways from Overpass (unique by lowercase name)
        var overpassWays = new Dictionary<string, (string Name, OverpassNode First, OverpassNode Last)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var way in overpass.Elements)
        {
            var name = way.Tags?.GetValueOrDefault("name");
            if (string.IsNullOrWhiteSpace(name) || way.Geometry == null || way.Geometry.Count < 2)
                continue;
            overpassWays.TryAdd(name, (name, way.Geometry.First(), way.Geometry.Last()));
        }

        // Load all streets currently linked to this zone
        var zoneStreets = await db.Streets
            .Where(s => s.GeoZoneId == zone.Id && s.TenantId == TenantId)
            .ToListAsync(ct);

        var zoneStreetsByName = zoneStreets.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        // Streets no longer returned by Overpass → soft-delete
        foreach (var existing in zoneStreets)
        {
            if (!overpassWays.ContainsKey(existing.Name))
                existing.IsDeleted = true;
        }

        // Streets returned by Overpass
        int created = 0, skipped = 0;
        int overpassTotal = overpassWays.Count;

        // Also need names already in DB from OTHER zones to avoid cross-zone duplicates
        var otherTenantNames = await db.Streets
            .Where(s => s.TenantId == TenantId && s.GeoZoneId != zone.Id)
            .Select(s => s.Name.ToLower())
            .ToHashSetAsync(ct);

        foreach (var (key, way) in overpassWays)
        {
            if (zoneStreetsByName.TryGetValue(way.Name, out var existing))
            {
                // Already belongs to this zone — update coordinates, keep risk values
                existing.IsDeleted = false;
                existing.StartLatitude = way.First.Lat;
                existing.StartLongitude = way.First.Lon;
                existing.EndLatitude = way.Last.Lat;
                existing.EndLongitude = way.Last.Lon;
                existing.City = zone.Name;
                skipped++;
            }
            else if (otherTenantNames.Contains(way.Name.ToLower()))
            {
                // Already exists under another zone — skip to avoid duplicate
                skipped++;
            }
            else
            {
                db.Streets.Add(new Street
                {
                    TenantId = TenantId,
                    GeoZoneId = zone.Id,
                    Name = way.Name,
                    City = zone.Name,
                    BaseRiskScore = 5,
                    RiskGrowthRatePerHour = 1,
                    CurrentRiskScore = 5,
                    StartLatitude = way.First.Lat,
                    StartLongitude = way.First.Lon,
                    EndLatitude = way.Last.Lat,
                    EndLongitude = way.Last.Lon
                });
                created++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new DetectStreetsResult(created, skipped, overpassTotal, null);
    }

    // ── Private Overpass DTOs ──────────────────────────────────────────────────

    private class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; } = [];
    }

    private class OverpassElement
    {
        public Dictionary<string, string>? Tags { get; set; }
        public List<OverpassNode>? Geometry { get; set; }
    }

    private class OverpassNode
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
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

public record DetectStreetsResult(int Created, int Skipped, int OverpassTotal, string? Error);
