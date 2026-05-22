using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Services;

public static class MissionWordGenerator
{
    private const string Blue   = "1E3A8A";
    private const string Gray   = "64748B";
    private const string Green  = "15803D";
    private const string Red    = "B91C1C";
    private const string Amber  = "92400E";
    private const string Bg     = "F1F5F9";
    private const string Border = "CBD5E1";

    public static byte[] Generate(MissionDto mission, DateTime generatedAt)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.AppendChild(body);

            body.AppendChild(Para("PrediCop — Rapport de Mission", 28, bold: true, color: "1A2035"));
            body.AppendChild(Para($"{mission.Reference}  •  {StatusLabel(mission.Status)}", 20, color: Blue));
            body.AppendChild(HRule(Blue));

            body.AppendChild(SectionTitle("Informations générales", Blue));
            body.AppendChild(KeyValueTable(BuildInfoRows(mission)));

            if (!string.IsNullOrEmpty(mission.BriefingText))
            {
                body.AppendChild(SectionTitle("Briefing", Blue));
                body.AppendChild(ShadedPara(mission.BriefingText));
            }

            if (!string.IsNullOrEmpty(mission.NarrativeReport))
            {
                body.AppendChild(SectionTitle("Rapport narratif", Blue));
                body.AppendChild(ShadedPara(mission.NarrativeReport));
            }

            if (!string.IsNullOrEmpty(mission.CompletionReport))
            {
                body.AppendChild(SectionTitle("Rapport de clôture", Green));
                body.AppendChild(ShadedPara(mission.CompletionReport));
            }

            if (mission.Intervenants.Count > 0)
            {
                body.AppendChild(SectionTitle("Intervenants", Blue));
                body.AppendChild(IntervenantsTable(mission.Intervenants.OrderBy(i => i.Order).ToList()));
            }

            if (mission.Assignments.Count > 0)
            {
                body.AppendChild(SectionTitle("Timeline des propositions", Amber));
                body.AppendChild(AssignmentsTable(mission.Assignments.OrderBy(a => a.ProposalOrder).ToList()));
            }

            body.AppendChild(Para(
                $"PrediCop — Document confidentiel — Généré le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}",
                14, italic: true, color: Gray));

            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134 }
            ));

            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    private static List<(string, string)> BuildInfoRows(MissionDto m)
    {
        var rows = new List<(string, string)>
        {
            ("Référence", m.Reference),
            ("Statut", StatusLabel(m.Status)),
        };
        if (!string.IsNullOrEmpty(m.CallReference)) rows.Add(("Appel lié", m.CallReference));
        rows.Add(("Adresse cible", m.TargetAddress));
        if (!string.IsNullOrEmpty(m.LocationDetail)) rows.Add(("Détail lieu", m.LocationDetail));
        rows.Add(("Coordonnées GPS", $"{m.TargetLatitude:F6}, {m.TargetLongitude:F6}"));
        rows.Add(("Créée le", m.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        if (m.DispatchedAt.HasValue)
            rows.Add(("Dispatchée le", m.DispatchedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        if (m.AcceptedAt.HasValue)
            rows.Add(("Acceptée le", m.AcceptedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        if (m.ArrivedAt.HasValue)
            rows.Add(("Arrivée sur place", m.ArrivedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        if (m.CompletedAt.HasValue)
            rows.Add(("Terminée le", m.CompletedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
        if (!string.IsNullOrEmpty(m.AssignedVehicleCallSign))
            rows.Add(("Véhicule assigné", m.AssignedVehicleCallSign));
        return rows;
    }

    // ── Section builders ────────────────────────────────────────────────────

    private static Paragraph SectionTitle(string title, string colorHex)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new ParagraphBorders(
            new BottomBorder { Val = BorderValues.Single, Color = colorHex, Size = 8, Space = 3 }));
        pPr.AppendChild(new SpacingBetweenLines { Before = "280", After = "80" });
        para.AppendChild(pPr);
        var rPr = new RunProperties();
        rPr.AppendChild(new Bold());
        rPr.AppendChild(new Color { Val = colorHex });
        rPr.AppendChild(new FontSize { Val = "20" });
        para.AppendChild(new Run(rPr, new Text(title)));
        return para;
    }

    private static Paragraph HRule(string colorHex)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new ParagraphBorders(
            new BottomBorder { Val = BorderValues.Single, Color = colorHex, Size = 16, Space = 1 }));
        pPr.AppendChild(new SpacingBetweenLines { After = "160" });
        para.AppendChild(pPr);
        return para;
    }

    private static Paragraph ShadedPara(string text)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new Shading { Fill = Bg, Val = ShadingPatternValues.Clear, Color = "auto" });
        pPr.AppendChild(new Indentation { Left = "240", Right = "240" });
        pPr.AppendChild(new SpacingBetweenLines { Line = "280", LineRule = LineSpacingRuleValues.Auto });
        para.AppendChild(pPr);
        var rPr = new RunProperties();
        rPr.AppendChild(new FontSize { Val = "18" });
        para.AppendChild(new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return para;
    }

    // ── Tables ──────────────────────────────────────────────────────────────

    private static Table KeyValueTable(List<(string Label, string Value)> rows)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));
        foreach (var (label, value) in rows)
        {
            var row = new TableRow();
            row.AppendChild(KvCell(label, "2300", bold: false, color: Gray));
            row.AppendChild(KvCell(value, null, bold: true));
            table.AppendChild(row);
        }
        return table;
    }

    private static TableCell KvCell(string text, string? fixedWidth, bool bold, string? color = null)
    {
        var cellPr = new TableCellProperties(CellMargins());
        if (fixedWidth != null)
            cellPr.AppendChild(new TableCellWidth { Width = fixedWidth, Type = TableWidthUnitValues.Dxa });
        var rPr = new RunProperties();
        if (bold) rPr.AppendChild(new Bold());
        if (color != null) rPr.AppendChild(new Color { Val = color });
        rPr.AppendChild(new FontSize { Val = "18" });
        return new TableCell(cellPr,
            new Paragraph(new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static Table IntervenantsTable(List<MissionIntervenantDto> items)
    {
        var table = StyledTable();
        var hRow = new TableRow();
        foreach (var h in new[] { "Nom", "Rôle", "Téléphone", "Blessé" })
            hRow.AppendChild(HeaderCell(h, Blue));
        table.AppendChild(hRow);
        foreach (var iv in items)
        {
            var row = new TableRow();
            row.AppendChild(DataCell(iv.FullName, bold: true));
            row.AppendChild(DataCell(iv.Role ?? "—", color: Gray));
            row.AppendChild(DataCell(iv.PhoneNumber ?? "—"));
            row.AppendChild(DataCell(iv.IsInjured ? "Oui" : "Non", color: iv.IsInjured ? Red : Green, bold: true));
            table.AppendChild(row);
        }
        return table;
    }

    private static Table AssignmentsTable(List<MissionAssignmentDto> items)
    {
        var table = StyledTable();
        var hRow = new TableRow();
        foreach (var h in new[] { "#", "Véhicule", "Statut", "Proposé à", "Distance", "Motif de refus" })
            hRow.AppendChild(HeaderCell(h, Amber));
        table.AppendChild(hRow);
        foreach (var a in items)
        {
            var row = new TableRow();
            row.AppendChild(DataCell($"#{a.ProposalOrder}", bold: true, color: Blue));
            row.AppendChild(DataCell(a.VehicleCallSign));
            row.AppendChild(DataCell(StatusLabel(a.Status)));
            row.AppendChild(DataCell(a.ProposedAt.ToLocalTime().ToString("HH:mm:ss")));
            row.AppendChild(DataCell($"{a.DistanceAtProposal:F1} km"));
            row.AppendChild(DataCell(a.RefusalReason ?? "—",
                color: string.IsNullOrEmpty(a.RefusalReason) ? Gray : Red));
            table.AppendChild(row);
        }
        return table;
    }

    // ── Primitives ──────────────────────────────────────────────────────────

    private static Paragraph Para(string text, int fontSize = 18, bool bold = false,
        string? color = null, bool italic = false)
    {
        var rPr = new RunProperties();
        if (bold) rPr.AppendChild(new Bold());
        if (italic) rPr.AppendChild(new Italic());
        if (color != null) rPr.AppendChild(new Color { Val = color });
        rPr.AppendChild(new FontSize { Val = fontSize.ToString() });
        return new Paragraph(new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table StyledTable()
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = Border, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Color = Border, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Color = Border, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Color = Border, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = Border, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = Border, Size = 4 })));
        return table;
    }

    private static TableCell HeaderCell(string text, string bgColor)
    {
        var rPr = new RunProperties();
        rPr.AppendChild(new Bold());
        rPr.AppendChild(new Color { Val = "FFFFFF" });
        rPr.AppendChild(new FontSize { Val = "16" });
        return new TableCell(
            new TableCellProperties(
                new Shading { Fill = bgColor, Val = ShadingPatternValues.Clear, Color = "auto" },
                CellMargins()),
            new Paragraph(new Run(rPr, new Text(text))));
    }

    private static TableCell DataCell(string text, bool bold = false, string? color = null)
    {
        var rPr = new RunProperties();
        if (bold) rPr.AppendChild(new Bold());
        if (color != null) rPr.AppendChild(new Color { Val = color });
        rPr.AppendChild(new FontSize { Val = "16" });
        return new TableCell(
            new TableCellProperties(CellMargins()),
            new Paragraph(new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static TableCellMargin CellMargins() =>
        new(new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            new LeftMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
            new RightMargin { Width = "160", Type = TableWidthUnitValues.Dxa });

    private static string StatusLabel(string status) => status switch
    {
        "Pending"    => "En attente",
        "Proposed"   => "Proposée",
        "Accepted"   => "Acceptée",
        "InProgress" => "En cours",
        "Completed"  => "Terminée",
        "Cancelled"  => "Annulée",
        "Refused"    => "Refusée",
        _ => status
    };
}
