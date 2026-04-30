using GraNAS.Signaling.Services.Implementations;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Signaling.Services.Extensions;

public static class SignalingServicesCollectionExtensions
{
    public static IServiceCollection AddSignalingApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITurnCredentialService, TurnCredentialService>();
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddScoped<IAccessChecker, AccessChecker>();
        services.AddScoped<IDeviceService, DeviceService>();
        return services;
    }
}
