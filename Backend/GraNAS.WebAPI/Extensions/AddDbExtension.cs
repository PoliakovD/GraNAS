
using System;
using GraNAS.WebAPI.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GraNAS.WebAPI.Extensions;

public static class AddDbExtension
{
  public static void AddPostgreSql(this IHostApplicationBuilder builder)
  {
    // Добавление контекста базы данных PostgreSQL

    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrEmpty(connectionString))
      throw new Exception("Отсутствует строка подключения к БД");


    builder.Services.AddDbContext<AppDbContext>(options =>
      options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
  }
}
