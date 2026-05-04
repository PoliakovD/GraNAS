using GraNAS.Notifications.DAL.Repositories;
using GraNAS.Notifications.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Notifications.DAL.Extensions;

public static class DalServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationDal(this IServiceCollection services)
    {
        services.AddScoped<INotificationEventRepository, NotificationEventRepository>();
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();
        return services;
    }
}
