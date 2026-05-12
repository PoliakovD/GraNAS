using GraNAS.Auth.DAL.Repositories.Implementation;
using GraNAS.Auth.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Auth.DAL.Extensions;

public static class DalServiceCollectionExtensions
{
  public static IServiceCollection AddAuthDal(this IServiceCollection services)
  {
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
    return services;
  }
}
