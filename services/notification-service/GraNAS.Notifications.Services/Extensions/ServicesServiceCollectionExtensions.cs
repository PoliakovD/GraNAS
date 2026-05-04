using GraNAS.Notifications.Services.Implementations;
using GraNAS.Notifications.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Notifications.Services.Extensions;

public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddMemoryCache();
        services.AddScoped<INotificationIngestionService, NotificationIngestionService>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddScoped<IUserContactResolver, UserContactResolver>();
        services.AddScoped<AuthServiceClient>();
        return services;
    }
}
