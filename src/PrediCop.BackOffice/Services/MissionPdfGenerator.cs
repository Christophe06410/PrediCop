using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Services;

public static class MissionPdfGenerator
{
    static MissionPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private const string NavyDark  = "#1a2035";
    private const string NavyLight = "#64748b";
    private const string Blue      = "#3b82f6";
    private const string Green     = "#22c55e";
    private const string Red       = "#ef4444";
    private const string Amber     = "#f59e0b";
    private const string BgLight   = "#f8fafc";
    private const string Border    = "#e2e8f0";

    public static byte[] Generate(MissionDto mission, DateTime generatedAt, byte[]? mapImage = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(NavyDark));

                page.Header().Element(c => RenderHeader(c, mission));
                page.Content().PaddingTop(0.5f, Unit.Centimetre).Element(c => RenderContent(c, mission, mapImage));
                page.Footer().Element(c => RenderFooter(c, generatedAt));
            });
        }).GeneratePdf();
    }

    private static void RenderHeader(IContainer container, MissionDto mission)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("PrediCop").Bold().FontSize(22).FontColor(NavyDark);
                    inner.Item().Text("Police Municipale — Rapport de Mission")
                        .FontSize(11).FontColor(NavyLight);
                });
                row.ConstantItem(120).AlignRight().AlignBottom().Column(inner =>
                {
                    inner.Item().AlignRight().Text(mission.Reference).Bold().FontSize(14).FontColor(Blue);
                    inner.Item().AlignRight().Text(TranslateStatus(mission.Status))
                        .FontSize(10).FontColor(StatusColor(mission.Status));
                });
            });
            col.Item().PaddingTop(6).Height(2).Background(Blue);
        });
    }

    private static void RenderContent(IContainer container, MissionDto mission, byte[]? mapImage)
    {
        container.Column(col =>
        {
            // --- Informations principales ---
            col.Item().Element(c => Section(c, "Informations générales", Blue, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Référence", mission.Reference);
                    InfoRow(inner, "Statut", TranslateStatus(mission.Status));
                    if (!string.IsNullOrEmpty(mission.CallReference))
                        InfoRow(inner, "Appel lié", mission.CallReference);
                    InfoRow(inner, "Adresse cible", mission.TargetAddress);
                    if (!string.IsNullOrEmpty(mission.LocationDetail))
                        InfoRow(inner, "Détail lieu", mission.LocationDetail);
                    InfoRow(inner, "Coordonnées GPS",
                        $"{mission.TargetLatitude:F6}, {mission.TargetLongitude:F6}");
                    InfoRow(inner, "Créée le", mission.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.DispatchedAt.HasValue)
                        InfoRow(inner, "Dispatchée le", mission.DispatchedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.AcceptedAt.HasValue)
                        InfoRow(inner, "Acceptée le", mission.AcceptedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.ArrivedAt.HasValue)
                        InfoRow(inner, "Arrivée sur place", mission.ArrivedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.CompletedAt.HasValue)
                        InfoRow(inner, "Terminée le", mission.CompletedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (!string.IsNullOrEmpty(mission.AssignedVehicleCallSign))
                        InfoRow(inner, "Véhicule assigné", mission.AssignedVehicleCallSign);
                })));

            // --- Carte ---
            if (mapImage != null)
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Localisation", Blue, body =>
                    body.Image(mapImage).FitWidth()));
            }

            // --- Briefing ---
            if (!string.IsNullOrEmpty(mission.BriefingText))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Briefing", NavyDark, body =>
                    body.Background(BgLight).Padding(10).Text(mission.BriefingText)
                        .FontSize(9).LineHeight(1.5f)));
            }

            // --- Rapport narratif ---
            if (!string.IsNullOrEmpty(mission.NarrativeReport))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Rapport narratif", Blue, body =>
                    body.Background(BgLight).Padding(10).Text(mission.NarrativeReport)
                        .FontSize(9).LineHeight(1.5f)));
            }

            // --- Rapport de clôture ---
            if (!string.IsNullOrEmpty(mission.CompletionReport))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Rapport de clôture", Green, body =>
                    body.Background(BgLight).Padding(10).Text(mission.CompletionReport)
                        .FontSize(9).LineHeight(1.5f)));
            }

            // --- Intervenants ---
            if (mission.Intervenants.Count > 0)
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Intervenants", NavyDark, body =>
                    body.Column(inner =>
                    {
                        foreach (var iv in mission.Intervenants.OrderBy(i => i.Order))
                        {
                            inner.Item().PaddingBottom(4).Row(row =>
                            {
                                row.ConstantItem(20).AlignMiddle()
                                    .Text($"{iv.Order}.").FontSize(8).FontColor(NavyLight);
                                row.RelativeItem().Column(detail =>
                                {
                                    detail.Item().Row(r2 =>
                                    {
                                        r2.RelativeItem().Text(iv.FullName).Bold().FontSize(9);
                                        if (!string.IsNullOrEmpty(iv.Role))
                                            r2.ConstantItem(80).AlignRight()
                                                .Text(iv.Role).FontSize(8).FontColor(NavyLight);
                                    });
                                    if (!string.IsNullOrEmpty(iv.PhoneNumber) || iv.IsInjured || !string.IsNullOrEmpty(iv.Notes))
                                    {
                                        detail.Item().Text(
                                            string.Join("  |  ", new[]
                                            {
                                                iv.PhoneNumber,
                                                iv.IsInjured ? "⚠ Blessé" : null,
                                                iv.Notes
                                            }.Where(s => !string.IsNullOrEmpty(s))))
                                            .FontSize(8).FontColor(NavyLight);
                                    }
                                });
                            });
                        }
                    })));
            }

            // --- Timeline des propositions ---
            if (mission.Assignments.Count > 0)
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Timeline des propositions", Amber, body =>
                    body.Column(inner =>
                    {
                        var ordered = mission.Assignments.OrderBy(x => x.ProposalOrder).ToList();
                        foreach (var a in ordered)
                        {
                            inner.Item().PaddingBottom(4).Row(row =>
                            {
                                row.ConstantItem(28).AlignMiddle()
                                    .Background(NavyDark).Padding(4)
                                    .AlignCenter().Text($"#{a.ProposalOrder}")
                                    .FontSize(8).Bold().FontColor(Colors.White);

                                row.RelativeItem().PaddingLeft(8).Column(detail =>
                                {
                                    detail.Item().Row(r2 =>
                                    {
                                        r2.RelativeItem().Text($"Véhicule {a.VehicleCallSign}").Bold().FontSize(9);
                                        r2.ConstantItem(80).AlignRight()
                                            .Text(TranslateStatus(a.Status))
                                            .FontSize(8).FontColor(StatusColor(a.Status));
                                    });
                                    detail.Item().Text(
                                        $"Proposé : {a.ProposedAt.ToLocalTime():HH:mm:ss}  |  Distance : {a.DistanceAtProposal:F1} km")
                                        .FontSize(8).FontColor(NavyLight);
                                    if (a.RespondedAt.HasValue)
                                        detail.Item()
                                            .Text($"Répondu : {a.RespondedAt.Value.ToLocalTime():HH:mm:ss}")
                                            .FontSize(8).FontColor(NavyLight);
                                    if (!string.IsNullOrEmpty(a.RefusalReason))
                                        detail.Item().PaddingTop(2)
                                            .Text($"Motif de refus : {a.RefusalReason}")
                                            .FontSize(8).FontColor(Red).Italic();
                                });
                            });

                            if (a != ordered.Last())
                                inner.Item().PaddingBottom(4).Height(1).Background(Border);
                        }
                    })));
            }
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

    // Section reçoit Action<IContainer> pour éviter la capture du container externe
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

    private static string TranslateStatus(string status) => status switch
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

    private static string StatusColor(string status) => status switch
    {
        "Accepted" or "Completed" => Green,
        "Refused"  or "Cancelled" => Red,
        "InProgress"              => Blue,
        _                         => Amber
    };
}
