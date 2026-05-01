namespace GraNAS.Shared.LoggingService.RabbitMq;

public class RabbitMqSinkOptions
{
    public string Host { get; set; } = "localhost";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "logs_exchange";
    public string Service { get; set; } = "unknown";
    public string Environment { get; set; } = "Development";
}
