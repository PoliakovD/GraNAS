// Services/RabbitMqConsumer.cs
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using GraNAS.LogService.Data;
using GraNAS.LogService.Models;


namespace GraNAS.LogService.Services;

public class RabbitMqLogConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceProvider _services;

    public RabbitMqLogConsumer(IConfiguration configuration, IServiceProvider services)
    {
        _services = services;
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };
        // Создаём асинхронное подключение
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Создаём канал для потребления
        var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync("logs_exchange", ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync("logs_queue", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync("logs_queue", "logs_exchange", "log");

        // Настраиваем потребителя
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            try
            {
                var logEntry = JsonSerializer.Deserialize<LogEntry>(message);
                if (logEntry != null)
                {
                    using var scope = _services.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<LogDbContext>();
                    dbContext.Logs.Add(logEntry);
                    await dbContext.SaveChangesAsync();
                }
                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                // Логируем ошибку
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync("logs_queue", autoAck: false, consumer: consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
