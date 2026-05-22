using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Services;

public static class CallReportWordGenerator
{
    private const string DarkBlue = "1E3A8A";
    private const string Gray     = "64748B";
    private const string Green    = "15803D";
    private const string Red      = "B91C1C";
    private const string Bg       = "F1F5F9";
    private const string Border   = "CBD5E1";
    private const string RedBg    = "FEF2F2";

    public static byte[] Generate(CallDto call, List<MissionDto> missions, DateTime generatedAt)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.AppendChild(body);

            // ── Letterhead ───────────────────────────────────────────────
            body.AppendChild(Para("POLICE MUNICIPALE", 28, bold: true, color: DarkBlue));
            body.AppendChild(Para("Compte rendu d'intervention — Main courante", 18, italic: true, color: Gray));
            body.AppendChild(HRule(DarkBlue, thick: true));
            body.AppendChild(HRule(DarkBlue, thick: false));

            body.AppendChild(KeyValueTable(new List<(string, string)>
            {
                ("N° de dossier", call.Reference),
                ("Rédigé le", generatedAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm")),
                ("Par", call.OperatorName),
                ("Objet", string.IsNullOrWhiteSpace(call.IncidentCategory) ? "Non précisé" : call.IncidentCategory),
                ("Statut", CallStatusLabel(call.Status)),
            }));

            body.AppendChild(HRule(Border));

            // ── Section I ────────────────────────────────────────────────
            body.AppendChild(SectionTitle("I. CADRE ET CONTEXTE DE L'INTERVENTION"));

            var dateHeure = call.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm");
            var context = $"Le {dateHeure}, suite à un appel reçu au standard, " +
                $"des faits ont été signalés au {call.IncidentAddress}" +
                (string.IsNullOrWhiteSpace(call.IncidentAddressComplement) ? "" : $" ({call.IncidentAddressComplement})") +
                $". L'appelant, M./Mme {call.CallerName}, joignable au {call.CallerPhone}, a signalé les faits décrits ci-après.";
            body.AppendChild(Para(context, 18));

            var contextRows = new List<(string, string)>
            {
                ("Date et heure du fait", call.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
                ("Adresse de l'incident", call.IncidentAddress +
                    (string.IsNullOrWhiteSpace(call.IncidentAddressComplement) ? "" : $" — {call.IncidentAddressComplement}")),
                ("Catégorie", call.IncidentCategory),
                ("Appelant", $"{call.CallerName} — Tél. {call.CallerPhone}"),
            };
            if (call.IncidentLatitude.HasValue && call.IncidentLongitude.HasValue)
                contextRows.Add(("Coordonnées GPS",
                    $"{call.IncidentLatitude.Value:F6}, {call.IncidentLongitude.Value:F6}"));
            body.AppendChild(KeyValueTable(contextRows));

            // ── Section II ───────────────────────────────────────────────
            body.AppendChild(SectionTitle("II. FAITS CONSTATÉS"));
            body.AppendChild(ShadedPara(
                string.IsNullOrWhiteSpace(call.IncidentDescription)
                    ? "(Aucune description renseignée)"
                    : call.IncidentDescription));

            if (!string.IsNullOrWhiteSpace(call.ThirdParties))
            {
                body.AppendChild(Para("Tierces personnes mentionnées par l'appelant :", 17, bold: true, color: Gray));
                body.AppendChild(ShadedPara(call.ThirdParties));
            }
            if (!string.IsNullOrWhiteSpace(call.Notes))
            {
                body.AppendChild(Para("Observations complémentaires :", 17, bold: true, color: Gray));
                body.AppendChild(ShadedPara(call.Notes));
            }

            // ── Section III ──────────────────────────────────────────────
            body.AppendChild(SectionTitle("III. MESURES PRISES — MISSIONS DÉCLENCHÉES"));

            if (missions.Count == 0)
            {
                body.AppendChild(ShadedPara("Aucune mission n'a été créée à ce stade."));
            }
            else
            {
                foreach (var m in missions)
                {
                    body.AppendChild(Para($"Mission {m.Reference}  —  {MissionStatusLabel(m.Status)}",
                        18, bold: true, color: DarkBlue));

                    var mRows = new List<(string, string)>
                    {
                        ("Véhicule assigné", m.AssignedVehicleCallSign ?? "Non encore assigné"),
                        ("Adresse cible", m.TargetAddress),
                    };
                    if (!string.IsNullOrWhiteSpace(m.LocationDetail)) mRows.Add(("Détail lieu", m.LocationDetail));
                    mRows.Add(("Mission créée le", m.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                    if (m.DispatchedAt.HasValue)
                        mRows.Add(("Dispatchée le", m.DispatchedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                    if (m.AcceptedAt.HasValue)
                        mRows.Add(("Acceptée le", m.AcceptedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                    if (m.ArrivedAt.HasValue)
                        mRows.Add(("Arrivée sur place le", m.ArrivedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                    if (m.CompletedAt.HasValue)
                        mRows.Add(("Clôturée le", m.CompletedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                    body.AppendChild(KeyValueTable(mRows));

                    var refusals = m.Assignments.Where(a => a.Status is "Refused").ToList();
                    if (refusals.Count > 0)
                    {
                        body.AppendChild(Para(
                            $"{refusals.Count} véhicule(s) ayant refusé : " +
                            string.Join(", ", refusals.Select(a => a.VehicleCallSign)),
                            16, italic: true, color: Gray));
                    }

                    if (m.Intervenants.Count > 0)
                    {
                        body.AppendChild(Para("Personnes impliquées lors de l'intervention :",
                            17, bold: true, color: Gray));
                        body.AppendChild(IntervenantsTable(m.Intervenants.OrderBy(i => i.Order).ToList()));
                    }
                }
            }

            // ── Section IV ───────────────────────────────────────────────
            body.AppendChild(SectionTitle("IV. COMPTE RENDU D'INTERVENTION"));

            var hasContent = false;
            foreach (var m in missions)
            {
                if (!string.IsNullOrWhiteSpace(m.BriefingText))
                {
                    body.AppendChild(Para($"Briefing — {m.Reference} :", 17, bold: true, color: Gray));
                    body.AppendChild(ShadedPara(m.BriefingText));
                    hasContent = true;
                }
                if (!string.IsNullOrWhiteSpace(m.NarrativeReport))
                {
                    body.AppendChild(Para($"Rapport narratif — {m.Reference} :", 17, bold: true, color: Gray));
                    body.AppendChild(ShadedPara(m.NarrativeReport));
                    hasContent = true;
                }
                if (!string.IsNullOrWhiteSpace(m.CompletionReport))
                {
                    body.AppendChild(Para($"Rapport de clôture — {m.Reference} :", 17, bold: true, color: Green));
                    body.AppendChild(ShadedPara(m.CompletionReport));
                    hasContent = true;
                }
            }
            if (!hasContent)
                body.AppendChild(Para("Rédiger ici le compte rendu officiel de l'intervention.", 18, italic: true, color: Gray));

            // Blank lines for handwritten additions
            for (var i = 0; i < 5; i++) body.AppendChild(HRule(Border));

            // ── Section V ────────────────────────────────────────────────
            body.AppendChild(SectionTitle("V. CONCLUSIONS ET SUITES DONNÉES"));
            body.AppendChild(Para(BuildConclusion(call, missions), 18));
            for (var i = 0; i < 4; i++) body.AppendChild(HRule(Border));

            // ── Notes internes ───────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(call.InternalNotes))
            {
                body.AppendChild(Para("NOTES INTERNES — USAGE STRICTEMENT CONFIDENTIEL",
                    16, bold: true, color: Red));
                body.AppendChild(HRule(Red));
                body.AppendChild(ShadedPara(call.InternalNotes, RedBg));
            }

            // ── Signatures ───────────────────────────────────────────────
            body.AppendChild(SignatureTable());

            // ── Footer ───────────────────────────────────────────────────
            body.AppendChild(HRule(Border));
            body.AppendChild(Para(
                $"Police Municipale — Main courante {call.Reference} — Document confidentiel — " +
                $"Généré le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}",
                14, italic: true, color: Gray));

            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1029, Bottom = 1029, Left = 1257, Right = 1257 }
            ));

            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    // ── Section builders ────────────────────────────────────────────────────

    private static Paragraph SectionTitle(string title)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new ParagraphBorders(
            new BottomBorder { Val = BorderValues.Single, Color = DarkBlue, Size = 8, Space = 3 }));
        pPr.AppendChild(new SpacingBetweenLines { Before = "320", After = "80" });
        para.AppendChild(pPr);
        var rPr = new RunProperties();
        rPr.AppendChild(new Bold());
        rPr.AppendChild(new Color { Val = DarkBlue });
        rPr.AppendChild(new FontSize { Val = "19" });
        para.AppendChild(new Run(rPr, new Text(title)));
        return para;
    }

    private static Paragraph HRule(string colorHex, bool thick = false)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new ParagraphBorders(
            new BottomBorder { Val = BorderValues.Single, Color = colorHex, Size = (UInt32Value)(thick ? 16u : 4u), Space = 1 }));
        pPr.AppendChild(new SpacingBetweenLines { Before = "40", After = "40" });
        para.AppendChild(pPr);
        return para;
    }

    private static Paragraph ShadedPara(string text, string fillColor = Bg)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new Shading { Fill = fillColor, Val = ShadingPatternValues.Clear, Color = "auto" });
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
            row.AppendChild(KvCell(label, "2400", bold: false, color: Gray));
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
            hRow.AppendChild(HeaderCell(h, DarkBlue));
        table.AppendChild(hRow);
        foreach (var iv in items)
        {
            var row = new TableRow();
            row.AppendChild(DataCell(iv.FullName, bold: true));
            row.AppendChild(DataCell(iv.Role ?? "—", color: Gray));
            row.AppendChild(DataCell(iv.PhoneNumber ?? "—"));
            row.AppendChild(DataCell(iv.IsInjured ? "Oui" : "Non",
                color: iv.IsInjured ? Red : Green, bold: true));
            table.AppendChild(row);
        }
        return table;
    }

    private static Table SignatureTable()
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

        var row = new TableRow();

        var leftCell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Dxa },
                new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Color = Border, Size = 6 }),
                CellMargins()),
            Para("L'agent rédacteur", 16, color: Gray),
            Para("(Grade, nom, signature)", 14, italic: true, color: Border),
            Para("", 18),
            Para("", 18),
            Para($"Clos le {DateTime.Now.ToLocalTime():dd/MM/yyyy à HH:mm}", 14, color: Gray));

        var spacer = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "400", Type = TableWidthUnitValues.Dxa }),
            Para("", 18));

        var rightCell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Dxa },
                new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Color = Border, Size = 6 }),
                CellMargins()),
            Para("Visa du responsable hiérarchique", 16, color: Gray),
            Para("(Grade, nom, cachet du service)", 14, italic: true, color: Border),
            Para("", 18),
            Para("", 18));

        row.AppendChild(leftCell);
        row.AppendChild(spacer);
        row.AppendChild(rightCell);
        table.AppendChild(row);
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

    private static string BuildConclusion(CallDto call, List<MissionDto> missions)
    {
        if (missions.Count == 0)
            return "Aucune mission n'a été créée. Le signalement est enregistré en main courante.";

        var parts = new List<string>();
        var completed  = missions.Count(m => m.Status is "Completed");
        var inProgress = missions.Count(m => m.Status is "InProgress" or "Accepted");
        var refused    = missions.Count(m => m.Status is "Refused" or "Cancelled");

        if (completed  > 0) parts.Add($"{completed} mission(s) ont été clôturée(s).");
        if (inProgress > 0) parts.Add($"{inProgress} mission(s) sont toujours en cours d'intervention.");
        if (refused    > 0) parts.Add($"{refused} proposition(s) de mission ont été refusées.");

        parts.Add(call.Status is "Closed" or "closed"
            ? "Le dossier est clos."
            : "Le dossier reste ouvert, en attente de clôture.");
        parts.Add("Le présent rapport est conservé au service conformément aux procédures internes.");
        return string.Join(" ", parts);
    }

    private static string CallStatusLabel(string status) => status switch
    {
        "Open"           => "Ouvert",
        "InProgress"     => "En cours",
        "MissionCreated" => "Mission créée",
        "Closed"         => "Clôturé",
        _ => status
    };

    private static string MissionStatusLabel(string status) => status switch
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
