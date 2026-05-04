using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Shared.Messaging.Events;
using GraNAS.Shared.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GraNAS.Notifications.API.HostedServices;

public class NotificationConsumerService : BackgroundService
{
    private const string Queue = "granas_notifications_queue";

    private static readonly string[] BindingKeys =
    [
        "access.granted", "access.revoked", "access.lost", "share.revoked"
    ];

    private readonly RabbitMqOptions _opts;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationConsumerService(
        IOptions<RabbitMqOptions> opts,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumerService> logger)
    {
        _opts = opts.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.Host,
            Port = _opts.Port,
            UserName = _opts.Username,
            Password = _opts.Password,
            VirtualHost = _opts.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            _opts.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-max-length"] = 100000, ["x-overflow"] = "drop-head" },
            cancellationToken: stoppingToken);

        foreach (var key in BindingKeys)
            await _channel.QueueBindAsync(Queue, _opts.ExchangeName, key, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 50, false, stoppingToken);

        _logger.LogInformation(
            "NotificationConsumer started: queue={Queue}, bindings={Bindings}",
            Queue, string.Join(",", BindingKeys));

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(ea.Body.Span,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (envelope is null)
                {
                    _logger.LogWarning(
                        "NotificationConsumer: malformed message — dropping (deliveryTag={DeliveryTag})",
                        ea.DeliveryTag);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<INotificationIngestionService>();
                await ingestionService.HandleAsync(envelope, stoppingToken);
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "NotificationConsumer: malformed message — dropping (deliveryTag={DeliveryTag})",
                    ea.DeliveryTag);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationConsumer: handler failed — requeue");
                await Task.Delay(2000, stoppingToken);
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
