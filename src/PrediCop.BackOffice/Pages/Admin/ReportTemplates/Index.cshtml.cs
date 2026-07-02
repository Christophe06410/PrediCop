using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Admin.ReportTemplates;

[Authorize(Roles = "Admin,SuperAdmin")]
public class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<ReportTemplateItem> Templates { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        var items = await client.GetFromJsonAsync<List<ReportTemplateItem>>("/api/report-templates", ct);
        Templates = items ?? [];
    }
}

public class ReportTemplateItem
{
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string DefaultTitle { get; set; } = string.Empty;
    public bool IsCustomized { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
