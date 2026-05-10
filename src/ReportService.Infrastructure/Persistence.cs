using Microsoft.EntityFrameworkCore;
using ReportService.Application;
using ReportService.Domain;

namespace ReportService.Infrastructure;

public class ReportDbContext(DbContextOptions<ReportDbContext> options) : DbContext(options)
{
    public DbSet<Report> Reports => Set<Report>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Report>().HasIndex(x => x.AnalysisProcessId).IsUnique();
        b.Entity<Report>().OwnsMany(x => x.Components);
        b.Entity<Report>().OwnsMany(x => x.Risks);
        b.Entity<Report>().OwnsMany(x => x.Recommendations);
        b.Entity<Report>().OwnsOne(x => x.AiModelMetadata);
    }
}

public class EfReportRepository(ReportDbContext db) : IReportRepository
{
    public Task AddAsync(Report report, CancellationToken ct = default) => db.Reports.AddAsync(report, ct).AsTask();
    public Task<Report?> GetByAnalysisProcessIdAsync(Guid id, CancellationToken ct = default) => db.Reports.FirstOrDefaultAsync(x => x.AnalysisProcessId == id, ct);
    public Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Reports.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<List<Report>> ListAsync(ReportQueryFilter f, CancellationToken ct = default)
    {
        var q = db.Reports.AsQueryable();
        if (f.Status is not null) q = q.Where(x => x.Status == f.Status);
        if (f.AnalysisProcessId is not null) q = q.Where(x => x.AnalysisProcessId == f.AnalysisProcessId);
        if (f.CreatedAtFrom is not null) q = q.Where(x => x.CreatedAt >= f.CreatedAtFrom);
        if (f.CreatedAtTo is not null) q = q.Where(x => x.CreatedAt <= f.CreatedAtTo);
        return await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    }
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
