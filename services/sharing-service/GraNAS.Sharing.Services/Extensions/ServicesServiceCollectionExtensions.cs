using GraNAS.Sharing.Services.Implementations;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Sharing.Services.Extensions;

public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddSharingApplication(this IServiceCollection services)
    {
        services.AddScoped<IShareService, ShareService>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        return services;
    }
}
