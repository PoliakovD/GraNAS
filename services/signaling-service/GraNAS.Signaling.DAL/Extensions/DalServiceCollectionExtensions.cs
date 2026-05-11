using GraNAS.Signaling.DAL.Repositories.Implementation;
using GraNAS.Signaling.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Signaling.DAL.Extensions;

/// <summary>Extension-методы для регистрации репозиториев DAL сигналинга в DI-контейнере.</summary>
public static class DalServiceCollectionExtensions
{
    /// <summary>Регистрирует <see cref="IDeviceRepository"/> и <see cref="IDeviceFolderRepository"/> как Scoped.</summary>
    public static IServiceCollection AddSignalingDal(this IServiceCollection services)
    {
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDeviceFolderRepository, DeviceFolderRepository>();
        return services;
    }
}
