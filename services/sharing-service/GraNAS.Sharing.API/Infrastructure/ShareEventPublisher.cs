using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace GraNAS.Sharing.API.Infrastructure;

public class ShareEventPublisher : IShareEventPublisher, IAsyncDisposable
{
    private const string ExchangeName = "share_events";
    private const string RoutingKey = "share.revoked";

    private readonly ILogger<ShareEventPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _initialized;

    public ShareEventPublisher(IConfiguration config, ILogger<ShareEventPublisher> logger)
    {
        _logger = logger;
        _ = InitializeAsync(config);
    }

    private async Task InitializeAsync(IConfiguration config)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest"
            };
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ connection — share_revoked events will not be published");
        }
    }

    public async Task PublishShareRevokedAsync(Guid shareLinkId, Guid folderId, Guid ownerId)
    {
        if (!_initialized || _channel is null)
        {
            _logger.LogWarning("RabbitMQ not available — share_revoked event dropped for ShareLink {Id}", shareLinkId);
            return;
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                EventType = "share_revoked",
                ShareLinkId = shareLinkId,
                FolderId = folderId,
                OwnerId = ownerId,
                OccurredAt = DateTime.UtcNow
            });

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "UTF-8"
            };

            await _channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish share_revoked event for ShareLink {Id}", shareLinkId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
