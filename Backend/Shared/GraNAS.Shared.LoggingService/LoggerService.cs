using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

public interface ILoggerService
{
    Task LogInfo(string message, string? userId = null, object? additionalData = null);
    Task LogWarning(string message, string? userId = null, object? additionalData = null);
    Task LogError(string message, string? userId = null, object? additionalData = null);
}

public class LoggerService : ILoggerService
{
    private readonly IConnection _connection;
    private readonly string _serviceName;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoggerService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };
        // Создаём подключение асинхронно, но в конструкторе синхронно ожидаем результат
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _serviceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
    }

    public async Task LogInfo(string message, string? userId = null, object? additionalData = null)
        => await PublishLogAsync("Information", message, userId, additionalData);

    public async Task LogWarning(string message, string? userId = null, object? additionalData = null)
        => await PublishLogAsync("Warning", message, userId, additionalData);

    public async Task LogError(string message, string? userId = null, object? additionalData = null)
        => await PublishLogAsync("Error", message, userId, additionalData);

    private async Task PublishLogAsync(string level, string message, string? userId, object? additionalData)
    {
        using var channel = await _connection.CreateChannelAsync();
        // Объявляем обменник и очередь (можно вынести в отдельный метод)
        await channel.ExchangeDeclareAsync("logs_exchange", ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync("logs_queue", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync("logs_queue", "logs_exchange", "log");

        var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
        var logMessage = new
        {
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            Service = _serviceName,
            Level = level,
            Message = message,
            UserId = userId,
            AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : null
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logMessage));
        var properties = new BasicProperties(); // новый способ создания свойств в v7
        await channel.BasicPublishAsync("logs_exchange", "log", mandatory: false, properties, body);
    }
}
