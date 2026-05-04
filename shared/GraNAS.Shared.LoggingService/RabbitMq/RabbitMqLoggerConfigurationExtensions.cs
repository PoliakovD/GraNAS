using Serilog;
using Serilog.Configuration;

namespace GraNAS.Shared.LoggingService.RabbitMq;

public static class RabbitMqLoggerConfigurationExtensions
{
    public static LoggerConfiguration RabbitMq(
        this LoggerSinkConfiguration sinkConfiguration,
        RabbitMqSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(options);
        return sinkConfiguration.Sink(new RabbitMqLogSink(options));
    }
}
