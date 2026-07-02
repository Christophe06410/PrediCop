using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class CreateCallReportRequest
{
    public Guid CallId { get; set; }
    public ReportType Type { get; set; } = ReportType.Information;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Recipients { get; set; }
    public bool IsDraft { get; set; } = true;
}

public class UpdateCallReportRequest
{
    public ReportType? Type { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public int? Recipients { get; set; }
    public bool? IsDraft { get; set; }
}

public class CallReportResponse
{
    public Guid Id { get; set; }
    public Guid CallId { get; set; }
    public string CallReference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int Recipients { get; set; }
    public string[] RecipientLabels { get; set; } = [];
    public bool IsDraft { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
