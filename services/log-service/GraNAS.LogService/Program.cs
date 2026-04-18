using Elastic.Clients.Elasticsearch;
using Serilog;
using Serilog.Sinks.Elasticsearch;

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

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
