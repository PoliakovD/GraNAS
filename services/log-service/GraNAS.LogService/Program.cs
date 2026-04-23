using Elastic.Clients.Elasticsearch;
using GraNAS.LogService.Health;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
  var esUri = ctx.Configuration["Elasticsearch:Uri"]
              ?? throw new InvalidOperationException("Elasticsearch:Uri is not configured");

  cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GraNAS.LogService")
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(esUri))
    {
      AutoRegisterTemplate = true,
      IndexFormat = "granas-logs-{0:yyyy.MM.dd}"
    });
});

var esUri = builder.Configuration["Elasticsearch:Uri"]
            ?? throw new InvalidOperationException("Elasticsearch:Uri is not configured");

builder.Services.AddSingleton(new ElasticsearchClient(new Uri(esUri)));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
  options.AddFixedWindowLimiter("api", policy =>
  {
    policy.PermitLimit = 60;
    policy.Window = TimeSpan.FromMinutes(1);
    policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    policy.QueueLimit = 0;
  });

  options.OnRejected = async (context, token) =>
  {
    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse
    {
      Error = "too_many_requests",
      ErrorDescription = "Too many requests. Please try again later."
    });
  };
});

builder.Services.AddHealthChecks()
  .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
  .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ["ready"]);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
  Predicate = c => c.Tags.Contains("live")
}).AllowAnonymous().DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
  Predicate = c => c.Tags.Contains("ready")
}).AllowAnonymous().DisableRateLimiting();

app.Run();
