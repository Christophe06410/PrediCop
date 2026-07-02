namespace PrediCop.Core.DTOs;

public class ReportTemplateResponse
{
    public Guid? Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string DefaultTitle { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsCustomized { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpsertReportTemplateRequest
{
    public string DefaultTitle { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
