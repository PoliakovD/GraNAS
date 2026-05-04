using GraNAS.Signaling.DAL.Repositories.Implementation;
using GraNAS.Signaling.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Signaling.DAL.Extensions;

public static class DalServiceCollectionExtensions
{
    public static IServiceCollection AddSignalingDal(this IServiceCollection services)
    {
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        return services;
    }
}
