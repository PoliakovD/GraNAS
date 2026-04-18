using GraNAS.Metadata.Services.Implementations;
using GraNAS.Metadata.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Metadata.Services.Extensions;

public static class ServicesServiceCollectionExtensions
{
  public static IServiceCollection AddMetadataApplication(this IServiceCollection services)
  {
    services.AddScoped<IFolderService, FolderService>();
    return services;
  }
}
