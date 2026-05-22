using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Services;

/// <summary>
/// Génère un procès-verbal / compte rendu d'intervention conforme au format
/// des documents officiels des polices municipales françaises.
/// </summary>
public static class CallReportPdfGenerator
{
    // Palette sobre, ton officiel
    private const string Ink        = "#1a1a2e";
    private const string DarkBlue   = "#1e3a8a";
    private const string MidGray    = "#64748b";
    private const string LightGray  = "#f1f5f9";
    private const string RuleColor  = "#94a3b8";
    private const string RedMark    = "#b91c1c";
    private const string GreenMark  = "#15803d";

    public static byte[] Generate(CallDto call, List<MissionDto> missions, DateTime generatedAt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2.2f, Unit.Centimetre);
                page.MarginVertical(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(Ink));

                page.Header().Element(c => RenderLetterhead(c, call, generatedAt));
                page.Content().PaddingTop(0.4f, Unit.Centimetre).Element(c => RenderBody(c, call, missions));
                page.Footer().Element(c => RenderFooter(c, call, generatedAt));
            });
        }).GeneratePdf();
    }

    // ── En-tête — style papier à en-tête officiel ──────────────────────────────

    private static void RenderLetterhead(IContainer container, CallDto call, DateTime generatedAt)
    {
        container.Column(col =>
        {
            // Bandeau supérieur
            col.Item().Row(row =>
            {
                // Gauche : service émetteur
                row.RelativeItem().Column(left =>
                {
                    left.Item()
                        .Text("POLICE MUNICIPALE")
                        .Bold().FontSize(15).FontColor(DarkBlue).LetterSpacing(0.05f);
                    left.Item().PaddingTop(2)
                        .Text("Compte rendu d'intervention — Main courante")
                        .FontSize(9).FontColor(MidGray).Italic();
                });

                // Droite : références du document
                row.ConstantItem(185).Column(right =>
                {
                    right.Item().AlignRight()
                        .Text($"N° {call.Reference}")
                        .Bold().FontSize(12).FontColor(DarkBlue);
                    right.Item().PaddingTop(2).AlignRight()
                        .Text($"Rédigé le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}")
                        .FontSize(8).FontColor(MidGray);
                    right.Item().PaddingTop(1).AlignRight()
                        .Text($"Par : {call.OperatorName}")
                        .FontSize(8).FontColor(MidGray);
                });
            });

            // Double filet
            col.Item().PaddingTop(7).Height(2).Background(DarkBlue);
            col.Item().PaddingTop(2).Height(0.5f).Background(RuleColor);

            // Objet
            col.Item().PaddingTop(7).Row(row =>
            {
                row.AutoItem().Text("Objet : ").Bold().FontSize(9.5f);
                row.RelativeItem()
                    .Text(string.IsNullOrWhiteSpace(call.IncidentCategory)
                        ? "Non précisé"
                        : call.IncidentCategory)
                    .FontSize(9.5f);

                // Statut à droite
                var (statusLabel, statusColor) = CallStatusDisplay(call.Status);
                row.ConstantItem(90).AlignRight()
                    .Text(statusLabel)
                    .Bold().FontSize(8.5f).FontColor(statusColor);
            });

            col.Item().PaddingTop(5).Height(0.5f).Background(RuleColor);
        });
    }

    // ── Corps du document ───────────────────────────────────────────────────────

    private static void RenderBody(IContainer container, CallDto call, List<MissionDto> missions)
    {
        container.Column(col =>
        {
            // Section I — Contexte de l'intervention
            col.Item().Element(c => SectionHeader(c, "I. CADRE ET CONTEXTE DE L'INTERVENTION"));

            col.Item().PaddingTop(5).Column(inner =>
            {
                // Paragraphe narratif auto-généré
                var dateHeure = call.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm");
                inner.Item().Text(text =>
                {
                    text.Span($"Le {dateHeure}, suite à un appel reçu au standard, nous avons été informés ")
                        .FontSize(9).LineHeight(1.6f);
                    text.Span($"de faits signalés au {call.IncidentAddress}").Bold().FontSize(9);
                    if (!string.IsNullOrWhiteSpace(call.IncidentAddressComplement))
                        text.Span($" ({call.IncidentAddressComplement})").FontSize(9);
                    text.Span($". L'appelant, M./Mme {call.CallerName}, joignable au {call.CallerPhone}, ")
                        .FontSize(9).LineHeight(1.6f);
                    text.Span("a signalé les faits décrits ci-après.").FontSize(9);
                });

                // Tableau récap
                inner.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });

                    TableRow(table, "Date et heure du fait", call.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    TableRow(table, "Adresse de l'incident", call.IncidentAddress +
                        (string.IsNullOrWhiteSpace(call.IncidentAddressComplement) ? "" : $" — {call.IncidentAddressComplement}"));
                    TableRow(table, "Catégorie", call.IncidentCategory);
                    TableRow(table, "Appelant", $"{call.CallerName} — Tél. {call.CallerPhone}");
                    if (call.IncidentLatitude.HasValue && call.IncidentLongitude.HasValue)
                        TableRow(table, "Coordonnées GPS",
                            $"{call.IncidentLatitude.Value:F6}, {call.IncidentLongitude.Value:F6}");
                });
            });

            // Section II — Faits constatés
            col.Item().PaddingTop(14).Element(c => SectionHeader(c, "II. FAITS CONSTATÉS"));

            col.Item().PaddingTop(5).Column(inner =>
            {
                inner.Item().Background(LightGray).Padding(9)
                    .Text(string.IsNullOrWhiteSpace(call.IncidentDescription)
                        ? "(Aucune description renseignée)"
                        : call.IncidentDescription)
                    .FontSize(9).LineHeight(1.65f);

                if (!string.IsNullOrWhiteSpace(call.ThirdParties))
                {
                    inner.Item().PaddingTop(8)
                        .Text("Tierces personnes mentionnées par l'appelant :")
                        .Bold().FontSize(8.5f).FontColor(MidGray);
                    inner.Item().PaddingTop(3).Background(LightGray).Padding(9)
                        .Text(call.ThirdParties).FontSize(9).LineHeight(1.65f);
                }

                if (!string.IsNullOrWhiteSpace(call.Notes))
                {
                    inner.Item().PaddingTop(8)
                        .Text("Observations complémentaires :")
                        .Bold().FontSize(8.5f).FontColor(MidGray);
                    inner.Item().PaddingTop(3).Background(LightGray).Padding(9)
                        .Text(call.Notes).FontSize(9).LineHeight(1.65f);
                }
            });

            // Section III — Mesures prises / Missions
            col.Item().PaddingTop(14).Element(c => SectionHeader(c, "III. MESURES PRISES — MISSIONS DÉCLENCHÉES"));

            col.Item().PaddingTop(5).Column(missionsCol =>
            {
                if (missions.Count == 0)
                {
                    missionsCol.Item().Background(LightGray).Padding(9)
                        .Text("Aucune mission n'a été créée à ce stade.")
                        .FontSize(9).Italic().FontColor(MidGray);
                }
                else
                {
                    foreach (var (m, idx) in missions.Select((m, i) => (m, i)))
                    {
                        if (idx > 0)
                            missionsCol.Item().PaddingVertical(8).Height(0.5f).Background(RuleColor);

                        // Sous-titre mission
                        missionsCol.Item().Row(row =>
                        {
                            row.AutoItem()
                                .Text($"Mission {m.Reference}")
                                .Bold().FontSize(9.5f).FontColor(DarkBlue);
                            row.RelativeItem();
                            var (mLabel, mColor) = MissionStatusDisplay(m.Status);
                            row.AutoItem()
                                .Text(mLabel).Bold().FontSize(8.5f).FontColor(mColor);
                        });

                        missionsCol.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });

                            TableRow(table, "Véhicule assigné", m.AssignedVehicleCallSign ?? "Non encore assigné");
                            TableRow(table, "Adresse cible", m.TargetAddress);
                            if (!string.IsNullOrWhiteSpace(m.LocationDetail))
                                TableRow(table, "Détail lieu", m.LocationDetail);
                            TableRow(table, "Mission créée le", m.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            if (m.DispatchedAt.HasValue)
                                TableRow(table, "Dispatchée le", m.DispatchedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            if (m.AcceptedAt.HasValue)
                                TableRow(table, "Acceptée le", m.AcceptedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            if (m.ArrivedAt.HasValue)
                                TableRow(table, "Arrivée sur place le", m.ArrivedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            if (m.CompletedAt.HasValue)
                                TableRow(table, "Clôturée le", m.CompletedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                        });

                        // Historique dispatch
                        var refusals = m.Assignments.Where(a => a.Status is "Refused").ToList();
                        if (refusals.Count > 0)
                        {
                            missionsCol.Item().PaddingTop(5)
                                .Text($"{refusals.Count} véhicule(s) ayant refusé : " +
                                    string.Join(", ", refusals.Select(a => a.VehicleCallSign)))
                                .FontSize(8).FontColor(MidGray).Italic();
                        }

                        // Intervenants
                        if (m.Intervenants.Count > 0)
                        {
                            missionsCol.Item().PaddingTop(7)
                                .Text("Personnes impliquées lors de l'intervention :")
                                .Bold().FontSize(8.5f).FontColor(MidGray);

                            missionsCol.Item().PaddingTop(3).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(20);
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                    c.ConstantColumn(40);
                                });

                                // Header
                                table.Header(h =>
                                {
                                    foreach (var label in new[] { "#", "Nom", "Rôle", "Téléphone", "Blessé" })
                                    {
                                        h.Cell().Background(DarkBlue).Padding(4)
                                            .Text(label).Bold().FontSize(7.5f).FontColor("white");
                                    }
                                });

                                foreach (var iv in m.Intervenants.OrderBy(i => i.Order))
                                {
                                    var shade = iv.Order % 2 == 0 ? "white" : LightGray;
                                    table.Cell().Background(shade).Padding(4).Text($"{iv.Order}").FontSize(8);
                                    table.Cell().Background(shade).Padding(4).Text(iv.FullName).Bold().FontSize(8);
                                    table.Cell().Background(shade).Padding(4).Text(iv.Role ?? "—").FontSize(8).FontColor(MidGray);
                                    table.Cell().Background(shade).Padding(4).Text(iv.PhoneNumber ?? "—").FontSize(8);
                                    table.Cell().Background(shade).Padding(4)
                                        .Text(iv.IsInjured ? "Oui" : "Non")
                                        .FontSize(8).FontColor(iv.IsInjured ? RedMark : GreenMark).Bold();
                                }
                            });
                        }
                    }
                }
            });

            // Section IV — Compte rendu d'intervention
            col.Item().PaddingTop(14).Element(c => SectionHeader(c, "IV. COMPTE RENDU D'INTERVENTION"));

            col.Item().PaddingTop(5).Column(inner =>
            {
                var hasContent = false;

                foreach (var m in missions)
                {
                    if (!string.IsNullOrWhiteSpace(m.BriefingText))
                    {
                        inner.Item().PaddingBottom(3)
                            .Text($"Briefing — {m.Reference} :").Bold().FontSize(8.5f).FontColor(MidGray);
                        inner.Item().PaddingBottom(6).Background(LightGray).Padding(9)
                            .Text(m.BriefingText).FontSize(9).LineHeight(1.65f);
                        hasContent = true;
                    }
                    if (!string.IsNullOrWhiteSpace(m.NarrativeReport))
                    {
                        inner.Item().PaddingBottom(3)
                            .Text($"Rapport narratif — {m.Reference} :").Bold().FontSize(8.5f).FontColor(MidGray);
                        inner.Item().PaddingBottom(6).Background(LightGray).Padding(9)
                            .Text(m.NarrativeReport).FontSize(9).LineHeight(1.65f);
                        hasContent = true;
                    }
                    if (!string.IsNullOrWhiteSpace(m.CompletionReport))
                    {
                        inner.Item().PaddingBottom(3)
                            .Text($"Rapport de clôture — {m.Reference} :").Bold().FontSize(8.5f).FontColor(GreenMark);
                        inner.Item().PaddingBottom(6)
                            .Border(0.5f).BorderColor(GreenMark)
                            .Background(LightGray).Padding(9)
                            .Text(m.CompletionReport).FontSize(9).LineHeight(1.65f);
                        hasContent = true;
                    }
                }

                if (!hasContent)
                {
                    inner.Item()
                        .Text("Rédiger ici le compte rendu officiel de l'intervention.")
                        .FontSize(9).Italic().FontColor(MidGray);
                }

                // Lignes de rédaction manuscrite
                inner.Item().PaddingTop(hasContent ? 10 : 5).Column(lines =>
                {
                    for (var i = 0; i < 6; i++)
                        lines.Item().PaddingTop(14).Height(0.5f).Background(RuleColor);
                });
            });

            // Section V — Conclusions
            col.Item().PaddingTop(14).Element(c => SectionHeader(c, "V. CONCLUSIONS ET SUITES DONNÉES"));

            col.Item().PaddingTop(5).Column(inner =>
            {
                // Conclusion auto-générée selon le statut
                var conclusion = BuildConclusion(call, missions);
                inner.Item().Text(conclusion).FontSize(9).LineHeight(1.65f);

                inner.Item().PaddingTop(10).Column(lines =>
                {
                    for (var i = 0; i < 4; i++)
                        lines.Item().PaddingTop(14).Height(0.5f).Background(RuleColor);
                });
            });

            // Notes internes (si présentes) — encadré confidentiel
            if (!string.IsNullOrWhiteSpace(call.InternalNotes))
            {
                col.Item().PaddingTop(14).Column(noteCol =>
                {
                    noteCol.Item().Row(row =>
                    {
                        row.AutoItem()
                            .Text("⬛ NOTES INTERNES — USAGE STRICTEMENT CONFIDENTIEL")
                            .Bold().FontSize(8.5f).FontColor(RedMark).LetterSpacing(0.03f);
                    });
                    noteCol.Item().PaddingTop(3).Height(1f).Background(RedMark);
                    noteCol.Item().PaddingTop(4)
                        .Border(1).BorderColor(RedMark)
                        .Background("#fef2f2").Padding(9)
                        .Text(call.InternalNotes).FontSize(9).LineHeight(1.65f).FontColor(RedMark);
                });
            }

            // Signatures
            col.Item().PaddingTop(20).Row(signRow =>
            {
                signRow.RelativeItem().Column(s =>
                {
                    s.Item().Text("L'agent rédacteur").FontSize(8).FontColor(MidGray);
                    s.Item().PaddingTop(2).Text("(Grade, nom, signature)").FontSize(7.5f).Italic().FontColor(RuleColor);
                    s.Item().PaddingTop(18).Height(0.5f).Background(RuleColor);
                    s.Item().PaddingTop(3).Text($"Rédigé et clos le {DateTime.Now.ToLocalTime():dd/MM/yyyy à HH:mm}")
                        .FontSize(7.5f).FontColor(MidGray);
                });
                signRow.ConstantItem(30);
                signRow.RelativeItem().Column(s =>
                {
                    s.Item().Text("Visa du responsable hiérarchique").FontSize(8).FontColor(MidGray);
                    s.Item().PaddingTop(2).Text("(Grade, nom, cachet du service)").FontSize(7.5f).Italic().FontColor(RuleColor);
                    s.Item().PaddingTop(18).Height(0.5f).Background(RuleColor);
                });
            });
        });
    }

    // ── Pied de page ────────────────────────────────────────────────────────────

    private static void RenderFooter(IContainer container, CallDto call, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(0.5f).Background(RuleColor);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Police Municipale — Main courante {call.Reference} — Document confidentiel")
                    .FontSize(7.5f).FontColor(MidGray);
                row.ConstantItem(150).AlignRight()
                    .Text(text =>
                    {
                        text.CurrentPageNumber().FontSize(7.5f).FontColor(MidGray);
                        text.Span(" / ").FontSize(7.5f).FontColor(MidGray);
                        text.TotalPages().FontSize(7.5f).FontColor(MidGray);
                    });
            });
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static void SectionHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(9.5f).FontColor(DarkBlue).LetterSpacing(0.02f);
            col.Item().PaddingTop(3).Height(1).Background(DarkBlue);
        });
    }

    private static void TableRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(3).PaddingLeft(0)
            .Text(label).FontSize(8.5f).FontColor(MidGray);
        table.Cell().Padding(3)
            .Text(value).FontSize(8.5f).Bold();
    }

    private static string BuildConclusion(CallDto call, List<MissionDto> missions)
    {
        if (missions.Count == 0)
            return "Aucune mission n'a été créée. Le signalement est enregistré en main courante.";

        var completed = missions.Count(m => m.Status is "Completed");
        var inProgress = missions.Count(m => m.Status is "InProgress" or "Accepted");
        var refused = missions.Count(m => m.Status is "Refused" or "Cancelled");

        var parts = new List<string>();

        if (completed > 0)
            parts.Add($"{completed} mission(s) ont été clôturée(s).");
        if (inProgress > 0)
            parts.Add($"{inProgress} mission(s) sont toujours en cours d'intervention.");
        if (refused > 0)
            parts.Add($"{refused} proposition(s) de mission ont été refusées.");

        if (call.Status is "Closed" or "closed")
            parts.Add("Le dossier est clos.");
        else
            parts.Add("Le dossier reste ouvert, en attente de clôture.");

        parts.Add("Le présent rapport est conservé au service conformément aux procédures internes.");

        return string.Join(" ", parts);
    }

    private static (string Label, string Color) CallStatusDisplay(string status) => status switch
    {
        "Open"           => ("OUVERT",         "#b45309"),
        "InProgress"     => ("EN COURS",        DarkBlue),
        "MissionCreated" => ("MISSION CRÉÉE",   DarkBlue),
        "Closed"         => ("CLÔTURÉ",         GreenMark),
        _                => (status.ToUpper(),  MidGray)
    };

    private static (string Label, string Color) MissionStatusDisplay(string status) => status switch
    {
        "Pending"    => ("EN ATTENTE",  MidGray),
        "Proposed"   => ("PROPOSÉE",    DarkBlue),
        "Accepted"   => ("ACCEPTÉE",    GreenMark),
        "InProgress" => ("EN COURS",    DarkBlue),
        "Completed"  => ("TERMINÉE",    GreenMark),
        "Refused"    => ("REFUSÉE",     RedMark),
        "Cancelled"  => ("ANNULÉE",     RedMark),
        _            => (status.ToUpper(), MidGray)
    };
}
