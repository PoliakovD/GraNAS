using GraNAS.Sharing.DAL.Repositories.Implementation;
using GraNAS.Sharing.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Sharing.DAL.Extensions;

public static class DalServiceCollectionExtensions
{
    public static IServiceCollection AddSharingDal(this IServiceCollection services)
    {
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
        return services;
    }
}
