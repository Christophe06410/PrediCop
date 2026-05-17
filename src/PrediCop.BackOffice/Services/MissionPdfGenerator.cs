using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Services;

public static class MissionPdfGenerator
{
    static MissionPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;
    private static readonly string NavyDark  = "#1a2035";
    private static readonly string NavyLight = "#64748b";
    private static readonly string Blue      = "#3b82f6";
    private static readonly string Green     = "#22c55e";
    private static readonly string Red       = "#ef4444";
    private static readonly string Amber     = "#f59e0b";
    private static readonly string BgLight   = "#f8fafc";
    private static readonly string Border    = "#e2e8f0";

    public static byte[] Generate(MissionDto mission, DateTime generatedAt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(NavyDark));

                page.Header().Element(c => Header(c, mission));
                page.Content().PaddingTop(0.5f, Unit.Centimetre).Element(c => Content(c, mission));
                page.Footer().Element(c => Footer(c, generatedAt));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, MissionDto mission)
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
                    inner.Item().AlignRight().Text(mission.Reference)
                        .Bold().FontSize(14).FontColor(Blue);
                    inner.Item().AlignRight().Text(TranslateStatus(mission.Status))
                        .FontSize(10).FontColor(StatusColor(mission.Status));
                });
            });

            col.Item().PaddingTop(6).Height(2).Background(Blue);
        });
    }

    private static void Content(IContainer container, MissionDto mission)
    {
        container.Column(col =>
        {
            // --- Informations principales ---
            col.Item().Element(c => Section(c, "Informations générales", Blue, () =>
            {
                c.Column(inner =>
                {
                    InfoRow(inner, "Référence", mission.Reference);
                    InfoRow(inner, "Statut", TranslateStatus(mission.Status));
                    if (!string.IsNullOrEmpty(mission.CallReference))
                        InfoRow(inner, "Appel lié", mission.CallReference);
                    InfoRow(inner, "Adresse cible", mission.TargetAddress);
                    InfoRow(inner, "Coordonnées GPS",
                        $"{mission.TargetLatitude:F6}, {mission.TargetLongitude:F6}");
                    InfoRow(inner, "Créée le",
                        mission.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.AcceptedAt.HasValue)
                        InfoRow(inner, "Acceptée le",
                            mission.AcceptedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (mission.CompletedAt.HasValue)
                        InfoRow(inner, "Terminée le",
                            mission.CompletedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    if (!string.IsNullOrEmpty(mission.AssignedVehicleCallSign))
                        InfoRow(inner, "Véhicule assigné", mission.AssignedVehicleCallSign);
                });
            }));

            // --- Briefing ---
            if (!string.IsNullOrEmpty(mission.BriefingText))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Briefing", NavyDark, () =>
                {
                    c.Background(BgLight).Padding(10).Text(mission.BriefingText)
                        .FontSize(9).LineHeight(1.5f);
                }));
            }

            // --- Rapport de clôture ---
            if (!string.IsNullOrEmpty(mission.CompletionReport))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Rapport de clôture", Green, () =>
                {
                    c.Background(BgLight).Padding(10).Text(mission.CompletionReport)
                        .FontSize(9).LineHeight(1.5f);
                }));
            }

            // --- Timeline des propositions ---
            if (mission.Assignments.Count > 0)
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Timeline des propositions", Amber, () =>
                {
                    c.Column(inner =>
                    {
                        foreach (var a in mission.Assignments.OrderBy(x => x.ProposalOrder))
                        {
                            inner.Item().PaddingBottom(4).Element(ac =>
                            {
                                ac.Row(row =>
                                {
                                    // Order badge
                                    row.ConstantItem(28).AlignMiddle()
                                        .Background(NavyDark).Padding(4)
                                        .AlignCenter().Text($"#{a.ProposalOrder}")
                                        .FontSize(8).Bold().FontColor(Colors.White);

                                    row.RelativeItem().PaddingLeft(8).Column(detail =>
                                    {
                                        detail.Item().Row(r2 =>
                                        {
                                            r2.RelativeItem().Text($"Véhicule {a.VehicleCallSign}")
                                                .Bold().FontSize(9);
                                            r2.ConstantItem(80).AlignRight()
                                                .Text(TranslateStatus(a.Status))
                                                .FontSize(8).FontColor(StatusColor(a.Status));
                                        });

                                        detail.Item().Text(
                                            $"Proposé : {a.ProposedAt.ToLocalTime():HH:mm:ss}" +
                                            $"  |  Distance : {a.DistanceAtProposal:F1} km")
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
                            });

                            if (a != mission.Assignments.OrderBy(x => x.ProposalOrder).Last())
                                inner.Item().PaddingBottom(4).Height(1).Background(Border);
                        }
                    });
                }));
            }
        });
    }

    private static void Footer(IContainer container, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(1).Background(Border);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("PrediCop — Document confidentiel")
                    .FontSize(8).FontColor(NavyLight);
                row.ConstantItem(200).AlignRight()
                    .Text($"Généré le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}")
                    .FontSize(8).FontColor(NavyLight);
            });
        });
    }

    private static void Section(IContainer container, string title, string accentColor, Action contentBuilder)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(4).Background(accentColor);
                row.RelativeItem().PaddingLeft(8).PaddingVertical(4)
                    .Text(title).Bold().FontSize(10).FontColor(NavyDark);
            });
            col.Item().PaddingTop(4).Element(_ => contentBuilder());
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
