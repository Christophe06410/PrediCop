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
public class MediaController(AppDbContext db, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;
    private Guid UserId => Guid.Parse(User.FindFirst("userId")!.Value);

    private string UploadsBasePath
    {
        get
        {
            var configured = config["MediaStorage:BasePath"];
            return string.IsNullOrEmpty(configured)
                ? Path.Combine(env.ContentRootPath, "uploads")
                : Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(env.ContentRootPath, configured);
        }
    }

    // GET /api/media?missionId=&documentId=
    [HttpGet]
    public async Task<ActionResult<List<MediaAttachmentResponse>>> GetAll(
        [FromQuery] Guid? missionId,
        [FromQuery] Guid? documentId,
        CancellationToken ct)
    {
        var query = db.MediaAttachments
            .Include(m => m.CreatedBy)
            .Include(m => m.Mission)
            .Include(m => m.Document)
            .Where(m => m.TenantId == TenantId);

        if (missionId.HasValue) query = query.Where(m => m.MissionId == missionId.Value);
        if (documentId.HasValue) query = query.Where(m => m.DocumentId == documentId.Value);

        var list = await query.OrderByDescending(m => m.RecordedAt).ToListAsync(ct);
        return Ok(list.Select(MapToResponse).ToList());
    }

    // GET /api/media/{id}/file
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken ct)
    {
        var attachment = await db.MediaAttachments
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (attachment is null) return NotFound();

        var fullPath = Path.Combine(UploadsBasePath, attachment.StoragePath);
        if (!System.IO.File.Exists(fullPath)) return NotFound("Fichier introuvable sur le serveur.");

        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, attachment.ContentType, attachment.FileName);
    }

    // POST /api/media
    [HttpPost]
    [RequestSizeLimit(500_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    public async Task<ActionResult<MediaAttachmentResponse>> Upload(
        [FromForm] UploadMediaRequest req, CancellationToken ct)
    {
        if (req.File is null || req.File.Length == 0)
            return Problem(title: "Fichier manquant ou vide", statusCode: 400);

        if (req.MissionId is null && req.DocumentId is null)
            return Problem(title: "MissionId ou DocumentId est requis", statusCode: 400);

        if (req.MissionId.HasValue)
        {
            var missionExists = await db.Missions.AnyAsync(
                m => m.Id == req.MissionId.Value && m.TenantId == TenantId, ct);
            if (!missionExists) return Problem(title: "Mission introuvable", statusCode: 404);
        }

        if (req.DocumentId.HasValue)
        {
            var docExists = await db.TrackingDocuments.AnyAsync(
                d => d.Id == req.DocumentId.Value && d.TenantId == TenantId, ct);
            if (!docExists) return Problem(title: "Document introuvable", statusCode: 404);
        }

        var yearMonth = req.RecordedAt.ToString("yyyy-MM");
        var relativeDir = Path.Combine(TenantId.ToString(), yearMonth);
        var ext = Path.GetExtension(req.File.FileName);
        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var relativePath = Path.Combine(relativeDir, storedFileName);

        var absoluteDir = Path.Combine(UploadsBasePath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(UploadsBasePath, relativePath);
        await using (var fs = System.IO.File.Create(absolutePath))
            await req.File.CopyToAsync(fs, ct);

        var attachment = new MediaAttachment
        {
            TenantId = TenantId,
            MissionId = req.MissionId,
            DocumentId = req.DocumentId,
            FileName = req.File.FileName,
            ContentType = req.File.ContentType,
            FileSizeBytes = req.File.Length,
            RecordedAt = req.RecordedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(req.RecordedAt, DateTimeKind.Utc)
                : req.RecordedAt.ToUniversalTime(),
            CameraDeviceId = req.CameraDeviceId,
            StoragePath = relativePath,
            CreatedByUserId = UserId
        };

        db.MediaAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        await db.Entry(attachment).Reference(m => m.CreatedBy).LoadAsync(ct);
        return Ok(MapToResponse(attachment));
    }

    // DELETE /api/media/{id}  — Manager/Admin only
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var attachment = await db.MediaAttachments
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (attachment is null) return NotFound();

        var fullPath = Path.Combine(UploadsBasePath, attachment.StoragePath);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        attachment.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MediaAttachmentResponse MapToResponse(MediaAttachment m) => new()
    {
        Id = m.Id,
        MissionId = m.MissionId,
        MissionReference = m.Mission?.Reference,
        DocumentId = m.DocumentId,
        DocumentReference = m.Document?.Reference,
        FileName = m.FileName,
        ContentType = m.ContentType,
        FileSizeBytes = m.FileSizeBytes,
        DurationSeconds = m.DurationSeconds,
        RecordedAt = m.RecordedAt,
        CameraDeviceId = m.CameraDeviceId,
        CreatedByUserId = m.CreatedByUserId,
        CreatedByName = m.CreatedBy?.FullName ?? string.Empty,
        CreatedAt = m.CreatedAt
    };
}

public class UploadMediaRequest
{
    public IFormFile? File { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? CameraDeviceId { get; set; }
}
