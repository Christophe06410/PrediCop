using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Tickets;

[Authorize(Roles = "Admin,Manager,Operator")]
public class PdfModel(IHttpClientFactory httpClientFactory) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");

        var ticket = await client.GetFromJsonAsync<ElectronicTicketResponse>(
            $"/api/tickets/{id}", JsonOpts, ct);

        if (ticket is null)
            return NotFound();

        var tenantName = User.FindFirstValue("tenantName") ?? "Police Municipale";
        var pdf = TicketPdfGenerator.Generate(ticket, tenantName, DateTime.UtcNow);

        return File(pdf, "application/pdf", $"{ticket.TicketNumber}.pdf");
    }
}
