using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController(AppDbContext db) : ControllerBase
{
    public record RegisterPushTokenRequest(string Token);

    [HttpPost("push-token")]
    public async Task<IActionResult> RegisterPushToken(
        [FromBody] RegisterPushTokenRequest request,
        CancellationToken ct)
    {
        var userIdStr = User.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();

        user.DeviceToken = request.Token;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
