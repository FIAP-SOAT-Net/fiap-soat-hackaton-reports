using NSubstitute;
using ReportService.Application;
using ReportService.Domain;
using Xunit;

namespace ReportService.Tests;

public class ReportServiceManagerTests
{
    private static CreateReportRequest PayloadValido(Guid? analysisProcessId = null) => new(
        analysisProcessId ?? Guid.NewGuid(),
        "diagrama.png",
        [new("API Gateway", "Gateway", "Ponto de entrada")],
        [new("SPOF", "High", "Sem redundancia", "Adicionar replicas")],
        [new("Circuit Breaker", "Implementar padrao circuit breaker")],
        new("OpenAI", "gpt-4o", "v1.0", 0.90m)
    );

    // --- Validate ---

    [Fact]
    public void Validate_PayloadValido_NaoLancaExcecao()
    {
        var ex = Record.Exception(() => ReportServiceManager.Validate(PayloadValido()));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_AnalysisProcessIdVazio_DeveFalhar()
    {
        var req = PayloadValido(Guid.Empty);
        var ex = Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
        Assert.Contains("analysisProcessId", ex.Message);
    }

    [Fact]
    public void Validate_SeverityInvalida_DeveFalhar()
    {
        var req = new CreateReportRequest(Guid.NewGuid(), "a.pdf", [], [new("t", "Invalida", "d", "r")], [], null);
        Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
    }

    [Fact]
    public void Validate_ListasVazias_DeveFalhar()
    {
        var req = new CreateReportRequest(Guid.NewGuid(), "a.pdf", [], [], [], null);
        var ex = Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
        Assert.Contains("lista", ex.Message);
    }

    [Fact]
    public void Validate_HtmlNoTexto_DeveFalhar()
    {
        var req = new CreateReportRequest(Guid.NewGuid(), "a.pdf",
            [new("<script>alert(1)</script>", "Type", "desc")], [], [], null);
        Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
    }

    [Fact]
    public void Validate_TextoMaiorQue500Chars_DeveFalhar()
    {
        var longo = new string('x', 501);
        var req = new CreateReportRequest(Guid.NewGuid(), "a.pdf",
            [new(longo, "Type", "desc")], [], [], null);
        Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
    }

    [Fact]
    public void Validate_SourceFileNameVazio_DeveFalhar()
    {
        var req = new CreateReportRequest(Guid.NewGuid(), "   ", [], [new("t", "High", "d", "r")], [], null);
        var ex = Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(req));
        Assert.Contains("sourceFileName", ex.Message);
    }

    // --- ToMarkdown ---

    [Fact]
    public void ExportMarkdown_DeveConterSecoesPrincipais()
    {
        var md = ReportServiceManager.ToMarkdown(new Report
        {
            SourceFileName = "diagrama.png",
            Components = [new() { Name = "API", Type = "Gateway", Description = "desc" }]
        });

        Assert.Contains("# Relatório Técnico", md);
        Assert.Contains("## Componentes identificados", md);
        Assert.Contains("## Riscos arquiteturais", md);
        Assert.Contains("## Recomendações", md);
        Assert.Contains("## Limitações", md);
    }

    [Fact]
    public void ExportMarkdown_ComRisco_DeveConterSeveridade()
    {
        var report = new Report
        {
            SourceFileName = "a.pdf",
            Risks = [new() { Title = "SPOF", Severity = RiskSeverity.Critical, Description = "desc", Recommendation = "rec" }]
        };

        var md = ReportServiceManager.ToMarkdown(report);

        Assert.Contains("SPOF", md);
        Assert.Contains("Critical", md);
    }

    [Fact]
    public void ExportMarkdown_ComAiModelInfo_DeveConterProvider()
    {
        var report = new Report
        {
            SourceFileName = "a.pdf",
            AiModelMetadata = new() { Provider = "OpenAI", Model = "gpt-4o", PromptVersion = "v1.0", Confidence = 0.9m }
        };

        var md = ReportServiceManager.ToMarkdown(report);

        Assert.Contains("OpenAI", md);
        Assert.Contains("gpt-4o", md);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_PayloadValido_CriaESalvaRelatorio()
    {
        var repo = Substitute.For<IReportRepository>();
        repo.GetByAnalysisProcessIdAsync(Arg.Any<Guid>()).Returns((Report?)null);

        var manager = new ReportServiceManager(repo);
        var req = PayloadValido();

        var result = await manager.CreateAsync(req);

        Assert.Equal(req.AnalysisProcessId, result.AnalysisProcessId);
        Assert.Equal(ReportStatus.Generated, result.Status);
        Assert.Single(result.Components);
        Assert.Single(result.Risks);
        Assert.Single(result.Recommendations);
        Assert.NotNull(result.AiModelMetadata);

        await repo.Received(1).AddAsync(Arg.Any<Report>());
        await repo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_AnalysisProcessIdDuplicado_LancaExcecao()
    {
        var repo = Substitute.For<IReportRepository>();
        repo.GetByAnalysisProcessIdAsync(Arg.Any<Guid>()).Returns(new Report());

        var manager = new ReportServiceManager(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateAsync(PayloadValido()));
    }

    [Fact]
    public async Task CreateAsync_PayloadInvalido_LancaExcecaoSemChamarRepo()
    {
        var repo = Substitute.For<IReportRepository>();
        var manager = new ReportServiceManager(repo);
        var req = new CreateReportRequest(Guid.Empty, "a.pdf", [], [], [], null);

        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateAsync(req));

        await repo.DidNotReceive().AddAsync(Arg.Any<Report>());
    }

    // --- Status enum ---

    [Theory]
    [InlineData("Pending")]
    [InlineData("Generated")]
    [InlineData("Failed")]
    public void StatusEnum_TodosOsValoresValidos(string status)
        => Assert.True(Enum.TryParse<ReportStatus>(status, true, out _));

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Critical")]
    public void RiskSeverityEnum_TodosOsValoresValidos(string severity)
        => Assert.True(Enum.TryParse<RiskSeverity>(severity, true, out _));
}
