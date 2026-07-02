using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class ReportTemplate : TenantEntity
{
    public ReportType Type { get; set; }
    public string DefaultTitle { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
