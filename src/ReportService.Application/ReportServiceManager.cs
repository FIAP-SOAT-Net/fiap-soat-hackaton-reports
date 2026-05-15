using System.Text;
using System.Text.RegularExpressions;
using ReportService.Domain;

namespace ReportService.Application;

public class ReportServiceManager(IReportRepository repository)
{
    private static readonly Regex HtmlRegex = new("<[^>]+>", RegexOptions.Compiled);
    private const int MaxListItems = 50;

    public async Task<Report> CreateAsync(CreateReportRequest request, CancellationToken ct = default)
    {
        Validate(request);
        if (await repository.GetByAnalysisProcessIdAsync(request.AnalysisProcessId, ct) is not null) throw new InvalidOperationException("DUPLICATE_REPORT");
        var report = new Report
        {
            AnalysisProcessId = request.AnalysisProcessId,
            SourceFileName = request.SourceFileName.Trim(),
            Status = ReportStatus.Generated,
            Components = request.Components.Select(c => new ReportComponent { Name = c.Name, Type = c.Type, Description = c.Description }).ToList(),
            Risks = request.Risks.Select(r => new ReportRisk { Title = r.Title, Severity = Enum.Parse<RiskSeverity>(r.Severity, true), Description = r.Description, Recommendation = r.Recommendation }).ToList(),
            Recommendations = request.Recommendations.Select(r => new ReportRecommendation { Title = r.Title, Description = r.Description }).ToList(),
            AiModelMetadata = request.AiModelInfo is null ? null : new AiModelMetadata { Provider = request.AiModelInfo.Provider, Model = request.AiModelInfo.Model, PromptVersion = request.AiModelInfo.PromptVersion, Confidence = request.AiModelInfo.Confidence }
        };
        await repository.AddAsync(report, ct); await repository.SaveChangesAsync(ct); return report;
    }

    public static string ToMarkdown(Report report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Relatório Técnico - {report.SourceFileName}");
        sb.AppendLine("## Resumo executivo");
        sb.AppendLine($"Status: {report.Status}; componentes: {report.Components.Count}; riscos: {report.Risks.Count}.");
        sb.AppendLine("## Componentes identificados");
        report.Components.ForEach(c => sb.AppendLine($"- **{c.Name}** ({c.Type}): {c.Description}"));
        sb.AppendLine("## Riscos arquiteturais");
        report.Risks.ForEach(r => sb.AppendLine($"- **{r.Title}** [{r.Severity}] - {r.Description}. Recomendação: {r.Recommendation}"));
        sb.AppendLine("## Recomendações");
        report.Recommendations.ForEach(r => sb.AppendLine($"- **{r.Title}**: {r.Description}"));
        sb.AppendLine("## Informações da análise de IA");
        sb.AppendLine(report.AiModelMetadata is null ? "Não informado." : $"Provider: {report.AiModelMetadata.Provider}; Model: {report.AiModelMetadata.Model}; Prompt: {report.AiModelMetadata.PromptVersion}; Confidence: {report.AiModelMetadata.Confidence}");
        sb.AppendLine("## Limitações"); sb.AppendLine("Análise automatizada sujeita a falso positivo/negativo.");
        return sb.ToString();
    }

    public static void Validate(CreateReportRequest request)
    {
        var details = new List<string>();
        if (request.AnalysisProcessId == Guid.Empty) details.Add("analysisProcessId é obrigatório");
        if (string.IsNullOrWhiteSpace(request.SourceFileName)) details.Add("sourceFileName é obrigatório");
        if ((request.Components?.Count ?? 0) + (request.Risks?.Count ?? 0) + (request.Recommendations?.Count ?? 0) == 0) details.Add("Ao menos uma lista deve ser preenchida");
        if ((request.Components?.Count ?? 0) > MaxListItems) details.Add($"components excede o limite de {MaxListItems} itens");
        if ((request.Risks?.Count ?? 0) > MaxListItems) details.Add($"risks excede o limite de {MaxListItems} itens");
        if ((request.Recommendations?.Count ?? 0) > MaxListItems) details.Add($"recommendations excede o limite de {MaxListItems} itens");
        foreach (var s in request.Risks.Select(r => r.Severity)) if (!Enum.TryParse<RiskSeverity>(s, true, out _)) details.Add($"severity inválida: {s}");
        IEnumerable<string> texts = [request.SourceFileName, ..request.Components.SelectMany(c => new[] { c.Name, c.Type, c.Description }), ..request.Risks.SelectMany(r => new[] { r.Title, r.Description, r.Recommendation }), ..request.Recommendations.SelectMany(r => new[] { r.Title, r.Description })];
        if (texts.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 500 || HtmlRegex.IsMatch(t))) details.Add("Campos textuais inválidos (vazio, HTML/script ou tamanho > 500)");
        if (details.Count > 0) throw new ArgumentException(string.Join(";", details));
    }
}
