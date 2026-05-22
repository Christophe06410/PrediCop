using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;

namespace PrediCop.BackOffice.Services;

public static class TicketPdfGenerator
{
    static TicketPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private const string NavyDark  = "#1a2035";
    private const string NavyLight = "#64748b";
    private const string Blue      = "#3b82f6";
    private const string Green     = "#16a34a";
    private const string Red       = "#dc2626";
    private const string Amber     = "#d97706";
    private const string BgLight   = "#f8fafc";
    private const string Border    = "#e2e8f0";

    public static byte[] Generate(ElectronicTicketResponse ticket, string tenantName, DateTime generatedAt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(NavyDark));

                page.Header().Element(c => RenderHeader(c, ticket, tenantName));
                page.Content().PaddingTop(0.5f, Unit.Centimetre).Element(c => RenderContent(c, ticket));
                page.Footer().Element(c => RenderFooter(c, generatedAt));
            });
        }).GeneratePdf();
    }

    // ── En-tête ──────────────────────────────────────────────────────────────

    private static void RenderHeader(IContainer container, ElectronicTicketResponse ticket, string tenantName)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("PrediCop").Bold().FontSize(22).FontColor(NavyDark);
                    inner.Item().Text($"Police Municipale — {tenantName}").FontSize(11).FontColor(NavyLight);
                    inner.Item().Text("PROCÈS-VERBAL D'INFRACTION").FontSize(10).Bold().FontColor(NavyDark);
                });
                row.ConstantItem(130).AlignRight().AlignBottom().Column(inner =>
                {
                    inner.Item().AlignRight().Text(ticket.TicketNumber)
                        .Bold().FontSize(16).FontColor(Red);
                    inner.Item().AlignRight().Text(TranslateStatus(ticket.Status))
                        .FontSize(10).FontColor(StatusColor(ticket.Status));
                    if (ticket.ExportedToAntai)
                        inner.Item().AlignRight().PaddingTop(2)
                            .Text("✓ Transmis ANTAI").FontSize(8).FontColor(Green);
                });
            });
            col.Item().PaddingTop(6).Height(3).Background(Red);
        });
    }

    // ── Corps ────────────────────────────────────────────────────────────────

    private static void RenderContent(IContainer container, ElectronicTicketResponse ticket)
    {
        container.Column(col =>
        {
            // Infraction
            col.Item().Element(c => Section(c, "Infraction constatée", Red, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Type d'infraction", TranslateInfraction(ticket.InfractionType));
                    if (!string.IsNullOrWhiteSpace(ticket.ArticleCode))
                        InfoRow(inner, "Article", ticket.ArticleCode);
                    InfoRow(inner, "Montant de l'amende", $"{ticket.FineAmount:F2} €");
                    InfoRow(inner, "Date et heure", ticket.IssuedAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm:ss"));
                })));

            // Agent
            col.Item().PaddingTop(12).Element(c => Section(c, "Agent verbalisateur", Blue, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Nom", ticket.IssuedByFullName);
                    InfoRow(inner, "Matricule", ticket.IssuedByBadgeNumber);
                })));

            // Lieu
            col.Item().PaddingTop(12).Element(c => Section(c, "Lieu de l'infraction", NavyDark, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Adresse", ticket.IssuedAtAddress);
                    if (ticket.Latitude.HasValue && ticket.Longitude.HasValue)
                        InfoRow(inner, "Coordonnées GPS",
                            $"{ticket.Latitude.Value:F6}, {ticket.Longitude.Value:F6}");
                })));

            // Véhicule
            col.Item().PaddingTop(12).Element(c => Section(c, "Véhicule contrevenant", NavyDark, body =>
                body.Column(inner =>
                {
                    InfoRow(inner, "Immatriculation", ticket.PlateNumber);
                    if (!string.IsNullOrWhiteSpace(ticket.VehicleMake))
                        InfoRow(inner, "Marque", ticket.VehicleMake!);
                    if (!string.IsNullOrWhiteSpace(ticket.VehicleModel))
                        InfoRow(inner, "Modèle", ticket.VehicleModel!);
                    if (!string.IsNullOrWhiteSpace(ticket.VehicleColor))
                        InfoRow(inner, "Couleur", ticket.VehicleColor!);
                })));

            // Observations
            if (!string.IsNullOrWhiteSpace(ticket.Notes))
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Observations", Amber, body =>
                    body.Background(BgLight).Padding(10)
                        .Text(ticket.Notes!).FontSize(9).LineHeight(1.5f)));
            }

            // Suivi ANTAI
            if (ticket.ExportedToAntai)
            {
                col.Item().PaddingTop(12).Element(c => Section(c, "Transmission ANTAI", Green, body =>
                    body.Column(inner =>
                    {
                        InfoRow(inner, "Exporté le",
                            ticket.ExportedAt.HasValue
                                ? ticket.ExportedAt.Value.ToLocalTime().ToString("dd/MM/yyyy à HH:mm")
                                : "—");
                        InfoRow(inner, "Statut", "Transmis à l'ANTAI");
                    })));
            }

            // Bloc de signature
            col.Item().PaddingTop(24).Element(c =>
            {
                c.Row(row =>
                {
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Border(1).BorderColor(Border).Padding(12)
                            .MinHeight(70).Text("Signature de l'agent :")
                            .FontSize(8).FontColor(NavyLight);
                    });
                    row.ConstantItem(20);
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Border(1).BorderColor(Border).Padding(12)
                            .MinHeight(70).Text("Signature du contrevenant :")
                            .FontSize(8).FontColor(NavyLight);
                    });
                });
            });
        });
    }

    // ── Pied de page ─────────────────────────────────────────────────────────

    private static void RenderFooter(IContainer container, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(1).Background(Border);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem()
                    .Text("PrediCop — Document officiel. Toute falsification est passible de poursuites.")
                    .FontSize(7).FontColor(NavyLight);
                row.ConstantItem(180).AlignRight()
                    .Text($"Généré le {generatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}")
                    .FontSize(7).FontColor(NavyLight);
            });
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Section(IContainer container, string title, string accent, Action<IContainer> body)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(4).Background(accent);
                row.RelativeItem().PaddingLeft(8).PaddingVertical(4)
                    .Text(title).Bold().FontSize(10).FontColor(NavyDark);
            });
            col.Item().PaddingTop(4).Element(body);
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

    private static string TranslateStatus(TicketStatus status) => status switch
    {
        TicketStatus.Issued    => "Émis",
        TicketStatus.Paid      => "Payé",
        TicketStatus.Contested => "Contesté",
        TicketStatus.Cancelled => "Annulé",
        _                      => status.ToString()
    };

    private static string StatusColor(TicketStatus status) => status switch
    {
        TicketStatus.Paid      => Green,
        TicketStatus.Cancelled => NavyLight,
        TicketStatus.Contested => Amber,
        _                      => Blue
    };

    private static string TranslateInfraction(InfractionType t) => t switch
    {
        InfractionType.StationnementInterdit    => "Stationnement interdit",
        InfractionType.StationnementGenant      => "Stationnement gênant",
        InfractionType.StationnementDangereux   => "Stationnement dangereux",
        InfractionType.StationnementHandicape   => "Stationnement emplacement handicapé",
        InfractionType.VitesseExcessive         => "Excès de vitesse",
        InfractionType.FeuRouge                 => "Non-respect feu rouge",
        InfractionType.NonRespectPriorite       => "Non-respect de la priorité",
        InfractionType.PortableAuVolant         => "Téléphone au volant",
        InfractionType.CeintureSecurity         => "Ceinture de sécurité non attachée",
        InfractionType.DefautAssurance          => "Défaut d'assurance",
        InfractionType.DefautControleTechnique  => "Défaut de contrôle technique",
        InfractionType.NuisanceSonore           => "Nuisance sonore",
        InfractionType.DegradationEspacePublic  => "Dégradation de l'espace public",
        InfractionType.Autre                    => "Autre infraction",
        _                                       => t.ToString()
    };
}
