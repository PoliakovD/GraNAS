using OpenSearch.Client;
using GraNAS.LogService.Health;
using GraNAS.LogService.Infrastructure;
using GraNAS.Shared.Models.DTO;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenSearch;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
  var esUri = ctx.Configuration["OpenSearch:Uri"]
              ?? throw new InvalidOperationException("OpenSearch:Uri is not configured");

  cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GraNAS.LogService")
    .WriteTo.Console()
    .WriteTo.OpenSearch(new OpenSearchSinkOptions(new Uri(esUri))
    {
      AutoRegisterTemplate = true,
      IndexFormat = "granas-logs-{0:yyyy.MM.dd}"
    });
});

var esUri = builder.Configuration["OpenSearch:Uri"]
            ?? throw new InvalidOperationException("OpenSearch:Uri is not configured");

builder.Services.AddSingleton(new OpenSearchClient(new ConnectionSettings(new Uri(esUri))));

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

builder.Services.AddHostedService<IndexTemplateInitializer>();
builder.Services.AddHostedService<LogIngestService>();

builder.Services.AddHealthChecks()
  .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
  .AddCheck<OpenSearchHealthCheck>("opensearch", tags: ["ready"])
  .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

var app = builder.Build();

app.UseSerilogRequestLogging(opts =>
{
  opts.GetLevel = (ctx, _, _) =>
    ctx.Request.Path.StartsWithSegments("/health")
      ? LogEventLevel.Debug
      : LogEventLevel.Information;
});
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

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
