namespace ReportService.Domain;

public enum ReportStatus { Pending, Generated, Failed }
public enum RiskSeverity { Low, Medium, High, Critical }

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisProcessId { get; set; }
    public Guid DiagramId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Generated;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ReportComponent> Components { get; set; } = [];
    public List<ReportRisk> Risks { get; set; } = [];
    public List<ReportRecommendation> Recommendations { get; set; } = [];
    public AiModelMetadata? AiModelMetadata { get; set; }
}

public class ReportComponent { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
public class ReportRisk { public Guid Id { get; set; } = Guid.NewGuid(); public string Title { get; set; } = string.Empty; public RiskSeverity Severity { get; set; } = RiskSeverity.Low; public string Description { get; set; } = string.Empty; public string Recommendation { get; set; } = string.Empty; }
public class ReportRecommendation { public Guid Id { get; set; } = Guid.NewGuid(); public string Title { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
public class AiModelMetadata { public Guid Id { get; set; } = Guid.NewGuid(); public string Provider { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; public string PromptVersion { get; set; } = string.Empty; public decimal Confidence { get; set; } }
