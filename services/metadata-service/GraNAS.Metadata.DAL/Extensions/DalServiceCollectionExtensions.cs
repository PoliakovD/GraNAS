using GraNAS.Metadata.DAL.Repositories.Implementation;
using GraNAS.Metadata.Models.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.Metadata.DAL.Extensions;

public static class DalServiceCollectionExtensions
{
  public static IServiceCollection AddMetadataDal(this IServiceCollection services)
  {
    services.AddScoped<IFolderRepository, FolderRepository>();
    return services;
  }
}
