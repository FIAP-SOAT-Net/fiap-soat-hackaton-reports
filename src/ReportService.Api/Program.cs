using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportService.Application;
using ReportService.Domain;
using ReportService.Infrastructure;
using Serilog;

var b = WebApplication.CreateBuilder(args);
b.Host.UseSerilog((ctx,cfg)=>cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());
var connectionString = b.Configuration.GetConnectionString("Default")
    ?? "Server=localhost;Port=3306;Database=reports;User=root;Password=root;";
b.Services.AddDbContext<ReportDbContext>(o =>
    o.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
b.Services.AddScoped<IReportRepository, EfReportRepository>();
b.Services.AddScoped<ReportServiceManager>();
b.Services.AddHealthChecks().AddDbContextCheck<ReportDbContext>();
b.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen(o => o.UseInlineDefinitionsForEnums());
var app = b.Build();
app.UseSwagger(); app.UseSwaggerUI();
app.Use(async (ctx,next)=>{ctx.Items["CorrelationId"]=ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()??Guid.NewGuid().ToString();await next();});
app.MapHealthChecks("/health", new HealthCheckOptions());
app.MapControllers();
app.UseExceptionHandler(a=>a.Run(async ctx=>{ctx.Response.StatusCode=500; await ctx.Response.WriteAsJsonAsync(new ErrorResponse("INTERNAL_ERROR","Erro interno.",[]));}));
using var scope = app.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ReportDbContext>().Database.Migrate();
app.Run();

public partial class Program { }
