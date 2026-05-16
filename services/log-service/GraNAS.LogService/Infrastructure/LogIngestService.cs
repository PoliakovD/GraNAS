using System.Text;
using OpenSearch.Client;
using OpenSearch.Net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqConnection = RabbitMQ.Client.IConnection;

namespace GraNAS.LogService.Infrastructure;

public sealed class LogIngestService : BackgroundService
{
    private const string Exchange = "logs_exchange";
    private const string Queue = "granas_logs_queue";

    private readonly IConfiguration _config;
    private readonly OpenSearchClient _os;
    private readonly ILogger<LogIngestService> _logger;
    private RabbitMqConnection? _connection;
    private IChannel? _channel;

    public LogIngestService(IConfiguration config, OpenSearchClient os, ILogger<LogIngestService> logger)
    {
        _config = config;
        _os = os;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost",
            UserName = _config["RabbitMQ:Username"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-max-length"] = 100000, ["x-overflow"] = "drop-head" },
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(Queue, Exchange, "logs.#", cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(0, 100, false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var utcNow = DateTime.UtcNow;
                var indexResult = await _os.LowLevel.IndexAsync<StringResponse>(
                    $"granas-logs-{utcNow:yyyy.MM.dd}",
                    PostData.String(json));

                if (indexResult.Success)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                else
                {
                    _logger.LogWarning("OpenSearch index failed: {Detail}", indexResult.DebugInformation);
                    await Task.Delay(2000);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing log message");
                await Task.Delay(2000);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(Queue, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
