using Microsoft.AspNetCore.Mvc;
using ReportService.Application;
using ReportService.Domain;

namespace ReportService.Api;

[ApiController]
[Route("api/reports")]
public class ReportsController(ReportServiceManager manager, IReportRepository repository, ILogger<ReportsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req, CancellationToken ct)
    {
        try { var r = await manager.CreateAsync(req, ct); logger.LogInformation("report created {ReportId}", r.Id); return CreatedAtAction(nameof(GetById), new { id = r.Id }, r); }
        catch (InvalidOperationException) { return Conflict(new ErrorResponse("REPORT_ALREADY_EXISTS", "Já existe relatório para analysisProcessId.", [])); }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponse("VALIDATION_ERROR", "Payload inválido.", ex.Message.Split(';').ToList())); }
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => await repository.GetByIdAsync(id, ct) is { } r ? Ok(r) : NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));
    [HttpGet("by-analysis/{analysisProcessId:guid}")] public async Task<IActionResult> ByAnalysis(Guid analysisProcessId, CancellationToken ct) => await repository.GetByAnalysisProcessIdAsync(analysisProcessId, ct) is { } r ? Ok(r) : NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));
    [HttpGet] public async Task<IActionResult> List([FromQuery] ReportStatus? status,[FromQuery] Guid? analysisProcessId,[FromQuery] DateTime? createdAtFrom,[FromQuery] DateTime? createdAtTo,CancellationToken ct) => Ok(await repository.ListAsync(new(status,analysisProcessId,createdAtFrom,createdAtTo), ct));
    [HttpGet("{id:guid}/export/markdown")] public async Task<IActionResult> Markdown(Guid id, CancellationToken ct) => await repository.GetByIdAsync(id, ct) is { } r ? Content(ReportServiceManager.ToMarkdown(r), "text/markdown") : NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));
    [HttpGet("{id:guid}/export/json")] public async Task<IActionResult> JsonExport(Guid id, CancellationToken ct) => await repository.GetByIdAsync(id, ct) is { } r ? Ok(r) : NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));

    [HttpGet("{id:guid}/export/pdf")]
    public async Task<IActionResult> PdfExport(Guid id, CancellationToken ct)
    {
        var r = await repository.GetByIdAsync(id, ct);
        if (r is null) return NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));
        var pdf = ReportPdfGenerator.Generate(r);
        var safeFileName = string.Concat(r.SourceFileName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        return File(pdf, "application/pdf", $"relatorio-{safeFileName}.pdf");
    }
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<ReportStatus>(req.Status, true, out var status)) return BadRequest(new ErrorResponse("VALIDATION_ERROR", "Status inválido.", []));
        var r = await repository.GetByIdAsync(id, ct); if (r is null) return NotFound(new ErrorResponse("REPORT_NOT_FOUND", "Relatório não encontrado.", []));
        r.Status = status; r.UpdatedAt = DateTime.UtcNow; await repository.SaveChangesAsync(ct); return Ok(r);
    }
}
