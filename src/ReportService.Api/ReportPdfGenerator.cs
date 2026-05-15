using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportService.Domain;

namespace ReportService.Api;

public static class ReportPdfGenerator
{
    private const string PrimaryColor  = "#1a1a2e";
    private const string AccentColor   = "#0f3460";
    private const string DangerColor   = "#c0392b";
    private const string WarningColor  = "#e67e22";
    private const string SuccessColor  = "#27ae60";
    private const string InfoColor     = "#2980b9";
    private const string SurfaceColor  = "#f8f9fa";
    private const string BorderColor   = "#dee2e6";

    public static byte[] Generate(Report report)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c));
                page.Content().Element(c => ComposeContent(c, report));
                page.Footer().Element(c => ComposeFooter(c));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container)
    {
        container
            .BorderBottom(2).BorderColor(AccentColor)
            .PaddingBottom(12)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FIAP Secure Systems").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().Text("Relatório de Análise Arquitetural")
                        .FontSize(18).Bold().FontColor(PrimaryColor);
                });
                row.ConstantItem(130).AlignRight().AlignMiddle()
                    .Text($"Gerado em\n{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
                    .FontSize(8).FontColor(Colors.Grey.Medium).AlignRight();
            });
    }

    private static void ComposeContent(IContainer container, Report report)
    {
        container.PaddingVertical(8).Column(col =>
        {
            col.Item().Element(c => ComposeSummaryCard(c, report));
            col.Item().Height(16);

            if (report.Components.Count > 0)
            {
                col.Item().Element(c => ComposeSectionTitle(c, "Componentes Identificados"));
                col.Item().Height(6);
                col.Item().Element(c => ComposeComponentsTable(c, report.Components));
                col.Item().Height(16);
            }

            if (report.Risks.Count > 0)
            {
                col.Item().Element(c => ComposeSectionTitle(c, "Riscos Arquiteturais"));
                col.Item().Height(6);
                foreach (var risk in report.Risks)
                {
                    col.Item().Element(c => ComposeRiskCard(c, risk));
                    col.Item().Height(6);
                }
                col.Item().Height(10);
            }

            if (report.Recommendations.Count > 0)
            {
                col.Item().Element(c => ComposeSectionTitle(c, "Recomendações"));
                col.Item().Height(6);
                col.Item().Element(c => ComposeRecommendations(c, report.Recommendations));
                col.Item().Height(16);
            }

            if (report.AiModelMetadata is not null)
            {
                col.Item().Element(c => ComposeSectionTitle(c, "Informações do Modelo de IA"));
                col.Item().Height(6);
                col.Item().Element(c => ComposeAiMetadata(c, report.AiModelMetadata));
                col.Item().Height(16);
            }

            col.Item().Element(c => ComposeDisclaimer(c));
        });
    }

    private static void ComposeSummaryCard(IContainer container, Report report)
    {
        container
            .Background(SurfaceColor)
            .Border(1).BorderColor(BorderColor)
            .Padding(16)
            .Column(col =>
            {
                col.Item().Text(report.SourceFileName).FontSize(13).Bold().FontColor(PrimaryColor);
                col.Item().Height(10);
                col.Item().Row(row =>
                {
                    SummaryBadge(row.RelativeItem(), "Status",        report.Status.ToString(),               AccentColor);
                    SummaryBadge(row.RelativeItem(), "Componentes",   report.Components.Count.ToString(),      InfoColor);
                    SummaryBadge(row.RelativeItem(), "Riscos",        report.Risks.Count.ToString(),           DangerColor);
                    SummaryBadge(row.RelativeItem(), "Recomendações", report.Recommendations.Count.ToString(), SuccessColor);
                });
            });
    }

    private static void SummaryBadge(IContainer container, string label, string value, string color)
    {
        container.PaddingRight(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Medium);
            col.Item().Text(value).FontSize(16).Bold().FontColor(color);
        });
    }

    private static void ComposeSectionTitle(IContainer container, string title)
    {
        container
            .BorderLeft(3).BorderColor(AccentColor)
            .PaddingLeft(8)
            .Text(title).FontSize(12).Bold().FontColor(PrimaryColor);
    }

    private static void ComposeComponentsTable(IContainer container, List<ReportComponent> components)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.RelativeColumn(5);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "Nome", "Tipo", "Descrição" })
                {
                    h.Cell().Background(AccentColor).Padding(6)
                        .Text(title).FontSize(9).Bold().FontColor(Colors.White);
                }
            });

            foreach (var (c, i) in components.Select((c, i) => (c, i)))
            {
                string bg = i % 2 == 0 ? Colors.White : SurfaceColor;
                table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(6)
                    .Text(c.Name).FontSize(9);
                table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(6)
                    .Text(c.Type).FontSize(9);
                table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(6)
                    .Text(c.Description).FontSize(9);
            }
        });
    }

    private static void ComposeRiskCard(IContainer container, ReportRisk risk)
    {
        var borderColor = risk.Severity switch
        {
            RiskSeverity.Critical => DangerColor,
            RiskSeverity.High     => DangerColor,
            RiskSeverity.Medium   => WarningColor,
            _                     => InfoColor
        };

        var severityLabel = risk.Severity switch
        {
            RiskSeverity.Critical => "CRÍTICO",
            RiskSeverity.High     => "ALTO",
            RiskSeverity.Medium   => "MÉDIO",
            _                     => "BAIXO"
        };

        container
            .Border(1).BorderColor(borderColor)
            .Padding(12)
            .Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(risk.Title).FontSize(10).Bold().FontColor(PrimaryColor);
                    row.ConstantItem(60).AlignRight()
                        .Background(borderColor).Padding(3)
                        .Text(severityLabel).FontSize(8).Bold().FontColor(Colors.White).AlignCenter();
                });
                col.Item().Height(4);
                col.Item().Text(risk.Description).FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Height(4);
                col.Item().Row(row =>
                {
                    row.ConstantItem(90)
                        .Text("Recomendação:").FontSize(9).Bold().FontColor(AccentColor);
                    row.RelativeItem()
                        .Text(risk.Recommendation).FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            });
    }

    private static void ComposeRecommendations(IContainer container, List<ReportRecommendation> recommendations)
    {
        container.Column(col =>
        {
            foreach (var rec in recommendations)
            {
                col.Item()
                    .Background(SurfaceColor).Border(1).BorderColor(BorderColor)
                    .Padding(10)
                    .Column(inner =>
                    {
                        inner.Item().Row(row =>
                        {
                            row.ConstantItem(20)
                                .Background(SuccessColor).AlignCenter().AlignMiddle()
                                .Text("✓").FontSize(9).Bold().FontColor(Colors.White);
                            row.ConstantItem(6);
                            row.RelativeItem()
                                .Text(rec.Title).FontSize(10).Bold().FontColor(PrimaryColor);
                        });
                        inner.Item().Height(4);
                        inner.Item().PaddingLeft(26)
                            .Text(rec.Description).FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                col.Item().Height(4);
            }
        });
    }

    private static void ComposeAiMetadata(IContainer container, AiModelMetadata meta)
    {
        container
            .Background(SurfaceColor).Border(1).BorderColor(BorderColor)
            .Padding(12)
            .Row(row =>
            {
                MetaField(row.RelativeItem(), "Provider",      meta.Provider);
                MetaField(row.RelativeItem(), "Modelo",        meta.Model);
                MetaField(row.RelativeItem(), "Versão Prompt", meta.PromptVersion);
                MetaField(row.RelativeItem(), "Confiança",     $"{meta.Confidence:P0}");
            });
    }

    private static void MetaField(IContainer container, string label, string value)
    {
        container.Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Medium);
            col.Item().Text(value).FontSize(10).Bold().FontColor(PrimaryColor);
        });
    }

    private static void ComposeDisclaimer(IContainer container)
    {
        container
            .Background("#fff3cd").Border(1).BorderColor("#ffc107")
            .Padding(10)
            .Text("Aviso: Este relatório foi gerado automaticamente por IA e pode conter imprecisões. Recomenda-se validação por especialistas antes de tomada de decisões arquiteturais.")
            .FontSize(8).FontColor("#856404").Italic();
    }

    private static void ComposeFooter(IContainer container)
    {
        container
            .BorderTop(1).BorderColor(BorderColor)
            .PaddingTop(6)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text("FIAP Secure Systems — Análise Arquitetural Automatizada")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
                row.ConstantItem(60).AlignRight()
                    .Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
    }
}
