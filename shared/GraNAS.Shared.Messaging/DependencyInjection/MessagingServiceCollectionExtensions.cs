using GraNAS.Shared.Messaging.Abstractions;
using GraNAS.Shared.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Shared.Messaging.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddGraNasMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RabbitMQ")
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(sectionName));
        services.AddHttpContextAccessor();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }
}
