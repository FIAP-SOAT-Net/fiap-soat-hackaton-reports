using ReportService.Application;
using ReportService.Domain;

namespace ReportService.Tests;

public class ReportServiceManagerTests
{
    [Fact] public void Validate_SeverityInvalida_DeveFalhar() => Assert.Throws<ArgumentException>(() => ReportServiceManager.Validate(new(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", [], [new("t","Bad","d","r")], [], null)));
    [Fact] public void ExportMarkdown_DeveConterSecoes()
    {
        var md = ReportServiceManager.ToMarkdown(new Report { SourceFileName = "a.pdf", Components = [new(){Name="API",Type="Gateway",Description="desc"}] });
        Assert.Contains("# Relatório Técnico", md);
        Assert.Contains("## Componentes identificados", md);
    }
    [Fact] public void UpdateStatusEnum_ParseValido() => Assert.True(Enum.TryParse<ReportStatus>("Generated", true, out _));
}
