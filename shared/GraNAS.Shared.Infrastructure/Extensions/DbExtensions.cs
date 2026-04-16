using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GraNAS.Shared.Infrastructure.Extensions;

public static class DbExtensions
{
  public static void AddPostgreSql<TContext>(this IHostApplicationBuilder builder)
    where TContext : DbContext
  {
    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrEmpty(connectionString))
      throw new Exception("Отсутствует строка подключения к БД");

    builder.Services.AddDbContext<TContext>(options =>
      options.UseNpgsql(connectionString));
  }
}
