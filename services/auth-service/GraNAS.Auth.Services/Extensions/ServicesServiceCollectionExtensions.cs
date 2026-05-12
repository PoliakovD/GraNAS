using GraNAS.Auth.Services.Implementations;
using GraNAS.Auth.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Auth.Services.Extensions;

public static class ServicesServiceCollectionExtensions
{
  public static IServiceCollection AddAuthApplication(this IServiceCollection services)
  {
    services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
    services.AddScoped<ITokenService, JwtTokenService>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IUserSettingsService, UserSettingsService>();
    return services;
  }
}
