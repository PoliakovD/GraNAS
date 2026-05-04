using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace GraNAS.Shared.LoggingService.RabbitMq;

public sealed class RabbitMqLogSink : ILogEventSink, IDisposable
{
    private readonly RabbitMqSinkOptions _opts;
    private readonly Lock _lock = new();
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _available;
    private bool _disposed;
    private readonly Timer _reconnectTimer;

    public RabbitMqLogSink(RabbitMqSinkOptions opts)
    {
        _opts = opts;
        TryConnect();
        _reconnectTimer = new Timer(_ => TryReconnectIfDown(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void TryConnect()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _opts.Host,
                    UserName = _opts.Username,
                    Password = _opts.Password
                };
                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
                _channel.ExchangeDeclareAsync(
                    exchange: _opts.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false).GetAwaiter().GetResult();
                _available = true;
                return;
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("RabbitMqLogSink connect attempt {0}/3 failed: {1}", attempt, ex.Message);
                if (attempt < 3)
                    Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
        _available = false;
    }

    private void TryReconnectIfDown()
    {
        lock (_lock)
        {
            if (_available || _disposed) return;
        }
        TryConnect();
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            if (!_available || _channel is null || _disposed)
            {
                SelfLog.WriteLine("RabbitMqLogSink unavailable — dropping event: {0}", logEvent.MessageTemplate.Text);
                return;
            }

            try
            {
                var doc = BuildDocument(logEvent);
                var body = JsonSerializer.SerializeToUtf8Bytes(doc);
                var routingKey = $"logs.{_opts.Service}.{logEvent.Level}";
                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    ContentEncoding = "UTF-8"
                };
                _channel.BasicPublishAsync(
                    exchange: _opts.ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _available = false;
                SelfLog.WriteLine("RabbitMqLogSink publish failed, marking unavailable: {0}", ex.Message);
            }
        }
    }

    private Dictionary<string, object?> BuildDocument(LogEvent logEvent)
    {
        var doc = new Dictionary<string, object?>
        {
            ["@timestamp"] = logEvent.Timestamp.UtcDateTime,
            ["Level"] = logEvent.Level.ToString(),
            ["Service"] = _opts.Service,
            ["Message"] = logEvent.RenderMessage(),
            ["Environment"] = _opts.Environment,
            ["Exception"] = logEvent.Exception?.ToString()
        };

        TryGetString(logEvent, "SourceContext", out var sc);
        doc["SourceContext"] = sc;

        TryGetString(logEvent, "ActionName", out var an);
        doc["ActionName"] = an;

        TryGetString(logEvent, "Method", out var method);
        doc["Method"] = method;

        TryGetString(logEvent, "CorrelationId", out var cid);
        doc["CorrelationId"] = cid;

        TryGetString(logEvent, "UserId", out var uid);
        doc["UserId"] = uid;

        if (logEvent.Properties.TryGetValue("Parameters", out var paramsProp))
            doc["Parameters"] = RenderPropertyValue(paramsProp);

        return doc;
    }

    private static bool TryGetString(LogEvent ev, string key, out string? value)
    {
        if (ev.Properties.TryGetValue(key, out var prop) && prop is ScalarValue sv)
        {
            value = sv.Value?.ToString();
            return true;
        }
        value = null;
        return false;
    }

    private static object? RenderPropertyValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue sv => sv.Value,
            StructureValue strv => strv.Properties.ToDictionary(p => p.Name, p => RenderPropertyValue(p.Value)),
            DictionaryValue dv => dv.Elements.ToDictionary(
                kv => kv.Key.Value?.ToString() ?? "",
                kv => RenderPropertyValue(kv.Value)),
            SequenceValue seq => seq.Elements.Select(RenderPropertyValue).ToArray(),
            _ => value.ToString()
        };
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _reconnectTimer.Dispose();
            _channel?.DisposeAsync().GetAwaiter().GetResult();
            _connection?.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
