using ReportService.Domain;

namespace ReportService.Application;

public record ErrorResponse(string Code, string Message, List<string> Details);
public record ReportQueryFilter(ReportStatus? Status, Guid? AnalysisProcessId, DateTime? CreatedAtFrom, DateTime? CreatedAtTo);
public record CreateReportRequest(Guid AnalysisProcessId, Guid DiagramId, string SourceFileName, List<ReportComponentDto> Components, List<ReportRiskDto> Risks, List<ReportRecommendationDto> Recommendations, AiModelMetadataDto? AiModelInfo);
public record ReportComponentDto(string Name, string Type, string Description);
public record ReportRiskDto(string Title, string Severity, string Description, string Recommendation);
public record ReportRecommendationDto(string Title, string Description);
public record AiModelMetadataDto(string Provider, string Model, string PromptVersion, decimal Confidence);
public record UpdateStatusRequest(string Status);
public record AnalysisCompletedEvent(Guid AnalysisProcessId, Guid DiagramId, string SourceFileName, List<ReportComponentDto> Components, List<ReportRiskDto> Risks, List<ReportRecommendationDto> Recommendations, AiModelMetadataDto? AiModelInfo);

public interface IAnalysisCompletedConsumer { Task ConsumeAsync(AnalysisCompletedEvent message, CancellationToken ct = default); }
public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Report?> GetByAnalysisProcessIdAsync(Guid analysisProcessId, CancellationToken ct = default);
    Task<List<Report>> ListAsync(ReportQueryFilter filter, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
