using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrediCop.BackOffice.Services;

public static class ShiftReportPdfGenerator
{
    static ShiftReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private const string NavyDark  = "#1a2035";
    private const string NavyLight = "#64748b";
    private const string Blue      = "#3b82f6";
    private const string Green     = "#22c55e";
    private const string Red       = "#ef4444";
    private const string Amber     = "#f59e0b";
    private const string BgLight   = "#f8fafc";
    private const string Border    = "#e2e8f0";

    public static byte[] Generate(ShiftReportPdfData data, DateTime generatedAt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(NavyDark));

                page.Header().Element(c => RenderHeader(c, data));
                page.Content().PaddingTop(0.5f, Unit.Centimetre).Element(c => RenderContent(c, data));
                page.Footer().Element(c => RenderFooter(c, generatedAt));
            });
        }).GeneratePdf();
    }

    private static void RenderHeader(IContainer container, ShiftReportPdfData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("PrediCop").Bold().FontSize(22).FontColor(NavyDark);
                    inner.Item().Text("Police Municipale — Rapport de fin de vacation")
                        .FontSize(11).FontColor(NavyLight);
                });
                row.ConstantItem(120).AlignRight().AlignBottom().Column(inner =>
                {
                    inner.Item().AlignRight().Text(data.VehicleCallSign).Bold().FontSize(14).FontColor(Blue);
                    inner.Item().AlignRight()
                        .Text(data.IsSigned ? "Signé" : "Non signé")
                        .FontSize(10).FontColor(data.IsSigned ? Green : Amber);
                });
            });
            col.Item().PaddingTop(6).Height(2).Background(Blue);
        });
    }

    private static void RenderContent(IContainer container, ShiftReportPdfData data)
    {
        var duration = data.ShiftEnd - data.ShiftStart;

        container.Column(col =>
        {
            // --- Informations vacation ---
            col.Item().Element(c => Section(c, "Informations de la vacation", Blue, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Véhicule", data.VehicleCallSign);
                    InfoRow(inner, "Agents", string.IsNullOrEmpty(data.OfficerNames) ? "—" : data.OfficerNames);
                    InfoRow(inner, "Début de service", data.ShiftStart.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    InfoRow(inner, "Fin de service", data.ShiftEnd.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    InfoRow(inner, "Durée totale", $"{(int)duration.TotalHours}h {duration.Minutes:D2}min");
                    if (data.IsSigned && data.SignedAt.HasValue)
                        InfoRow(inner, "Signé le", data.SignedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                })));

            // --- Statistiques missions ---
            col.Item().PaddingTop(12).Element(c => Section(c, "Statistiques des missions", Blue, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Missions reçues", data.MissionCount.ToString());
                    InfoRow(inner, "Missions terminées", data.CompletedMissionCount.ToString());
                    InfoRow(inner, "Missions refusées", data.RefusedMissionCount.ToString());
                })));

            // --- Patrouilles ---
            col.Item().PaddingTop(12).Element(c => Section(c, "Patrouilles", NavyDark, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Passages de rues", data.PatrolRecordCount.ToString());
                    InfoRow(inner, "Distance estimée", $"{data.EstimatedKm:F1} km");
                })));

            // --- Documents ---
            col.Item().PaddingTop(12).Element(c => Section(c, "Documents produits", NavyDark, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Nombre de documents", data.DocumentCount.ToString());
                })));

            // --- Notes ---
            if (!string.IsNullOrWhiteSpace(data.Notes))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Notes de l'agent", Amber, body =>
                    body.Background(BgLight).Padding(10).Text(data.Notes)
                        .FontSize(9).LineHeight(1.5f)));
            }

            // --- Signature ---
            col.Item().PaddingTop(20).Element(c =>
                c.Row(row =>
                {
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Text("Signature de l'agent").FontSize(9).FontColor(NavyLight);
                        sig.Item().PaddingTop(4).Height(1).Background(Border);
                        sig.Item().PaddingTop(2).Text(data.IsSigned && data.SignedAt.HasValue
                            ? $"Signé électroniquement le {data.SignedAt.Value.ToLocalTime():dd/MM/yyyy à HH:mm}"
                            : "Non signé").FontSize(8).FontColor(data.IsSigned ? Green : Red);
                    });
                    row.ConstantItem(40);
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Text("Visa du responsable").FontSize(9).FontColor(NavyLight);
                        sig.Item().PaddingTop(4).Height(1).Background(Border);
                        sig.Item().PaddingTop(2).Text(" ").FontSize(8);
                    });
                }));
        });
    }

    private static void RenderFooter(IContainer container, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(1).Background(Border);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("PrediCop — Document confidentiel").FontSize(8).FontColor(NavyLight);
                row.ConstantItem(200).AlignRight()
                    .Text($"Généré le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}")
                    .FontSize(8).FontColor(NavyLight);
            });
        });
    }

    private static void Section(IContainer container, string title, string accentColor, Action<IContainer> bodyBuilder)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(4).Background(accentColor);
                row.RelativeItem().PaddingLeft(8).PaddingVertical(4)
                    .Text(title).Bold().FontSize(10).FontColor(NavyDark);
            });
            col.Item().PaddingTop(4).Element(bodyBuilder);
        });
    }

    private static void InfoRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(130).Text(label).FontColor(NavyLight);
            row.RelativeItem().Text(value).Bold();
        });
    }
}

/// <summary>Données passées au générateur PDF pour le rapport de vacation.</summary>
public class ShiftReportPdfData
{
    public string VehicleCallSign { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public string OfficerNames { get; set; } = string.Empty;
    public int MissionCount { get; set; }
    public int CompletedMissionCount { get; set; }
    public int RefusedMissionCount { get; set; }
    public int PatrolRecordCount { get; set; }
    public double EstimatedKm { get; set; }
    public int DocumentCount { get; set; }
    public string? Notes { get; set; }
    public bool IsSigned { get; set; }
    public DateTime? SignedAt { get; set; }
}
