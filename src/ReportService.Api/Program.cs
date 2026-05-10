using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportService.Application;
using ReportService.Domain;
using ReportService.Infrastructure;
using Serilog;

var b = WebApplication.CreateBuilder(args);
b.Host.UseSerilog((ctx,cfg)=>cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());
b.Services.AddDbContext<ReportDbContext>(o => o.UseSqlite(b.Configuration.GetConnectionString("Default") ?? "Data Source=reports.db"));
b.Services.AddScoped<IReportRepository, EfReportRepository>();
b.Services.AddScoped<ReportServiceManager>();
b.Services.AddScoped<IAnalysisCompletedConsumer, AnalysisCompletedConsumer>();
b.Services.AddHealthChecks().AddDbContextCheck<ReportDbContext>();
b.Services.AddControllers(); b.Services.AddEndpointsApiExplorer(); b.Services.AddSwaggerGen();
var app = b.Build();
app.UseSwagger(); app.UseSwaggerUI();
app.Use(async (ctx,next)=>{ctx.Items["CorrelationId"]=ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()??Guid.NewGuid().ToString();await next();});
app.MapHealthChecks("/health", new HealthCheckOptions());
app.MapControllers();
app.UseExceptionHandler(a=>a.Run(async ctx=>{ctx.Response.StatusCode=500; await ctx.Response.WriteAsJsonAsync(new ErrorResponse("INTERNAL_ERROR","Erro interno.",[]));}));
using var scope = app.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ReportDbContext>().Database.EnsureCreated();
app.Run();

public partial class Program { }
