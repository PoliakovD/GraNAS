using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Shared.Messaging.Abstractions;
using GraNAS.Shared.Messaging.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace GraNAS.Shared.Messaging.RabbitMq;

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _opts;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly Lock _lock = new();
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _available;
    private bool _disposed;
    private readonly Timer _reconnectTimer;

    public RabbitMqEventPublisher(
        IOptions<RabbitMqOptions> opts,
        ILogger<RabbitMqEventPublisher> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _opts = opts.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _ = TryInitializeAsync();
        _reconnectTimer = new Timer(_ => { if (!_available && !_disposed) _ = TryInitializeAsync(); },
            null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private async Task TryInitializeAsync()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _opts.Host,
                    Port = _opts.Port,
                    UserName = _opts.Username,
                    Password = _opts.Password,
                    VirtualHost = _opts.VirtualHost
                };
                var connection = await factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();
                await channel.ExchangeDeclareAsync(
                    exchange: _opts.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                lock (_lock)
                {
                    _connection = connection;
                    _channel = channel;
                    _available = true;
                }

                _logger.LogInformation("Messaging: connected to RabbitMQ exchange={Exchange}", _opts.ExchangeName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Messaging: connect attempt {Attempt}/3 failed", attempt);
                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        lock (_lock) { _available = false; }
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        IChannel? channel;
        lock (_lock)
        {
            if (!_available || _channel is null || _disposed)
            {
                _logger.LogWarning(
                    "Messaging: channel unavailable — dropping {EventType} eventId={EventId}",
                    evt.EventType, evt.EventId);
                return;
            }
            channel = _channel;
        }

        try
        {
            var correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier;
            var envelope = new
            {
                eventId = evt.EventId,
                eventType = evt.EventType,
                occurredAt = evt.OccurredAt,
                correlationId,
                payload = evt
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
            var routingKey = EventRoutingConvention.GetRoutingKey<TEvent>();
            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "UTF-8",
                MessageId = evt.EventId.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: _opts.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            lock (_lock) { _available = false; }
            _logger.LogError(ex,
                "Messaging: publish failed for {EventType} eventId={EventId}",
                evt.EventType, evt.EventId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _available = false;
        }
        _reconnectTimer.Dispose();
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
