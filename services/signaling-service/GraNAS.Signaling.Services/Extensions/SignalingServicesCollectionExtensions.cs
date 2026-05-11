using GraNAS.Signaling.Services.Implementations;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Signaling.Services.Extensions;

/// <summary>Extension-методы для регистрации сервисов слоя GraNAS.Signaling.Services в DI-контейнере.</summary>
public static class SignalingServicesCollectionExtensions
{
    /// <summary>
    /// Регистрирует сервисы бизнес-логики сигналинга:
    /// <see cref="ITurnCredentialService"/> и <see cref="ISessionStore"/> — как Singleton,
    /// <see cref="IAccessChecker"/> и <see cref="IDeviceService"/> — как Scoped.
    /// </summary>
    public static IServiceCollection AddSignalingApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITurnCredentialService, TurnCredentialService>();
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddScoped<IAccessChecker, AccessChecker>();
        services.AddScoped<IDeviceService, DeviceService>();
        return services;
    }
}
