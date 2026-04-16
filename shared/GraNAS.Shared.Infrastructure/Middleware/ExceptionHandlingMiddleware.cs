using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using GraNAS.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GraNAS.Shared.Infrastructure.Middleware;

public class ExceptionHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<ExceptionHandlingMiddleware> _logger;

  public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await _next(context);
    }
    catch (Exception ex)
    {
      await HandleExceptionAsync(context, ex);
    }
  }

  private async Task HandleExceptionAsync(HttpContext context, Exception exception)
  {
    _logger.LogError(exception, "An unhandled exception occurred.");

    var response = context.Response;
    response.ContentType = "application/json";

    var errorResponse = new ErrorResponse();

    switch (exception)
    {
      case DbUpdateException dbEx:
        if (dbEx.InnerException is PostgresException postgresException && postgresException.SqlState == "23505")
        {
          response.StatusCode = (int)HttpStatusCode.Conflict;
          errorResponse.Error = "duplicate_key";
          errorResponse.ErrorDescription = "A record with the same unique key already exists.";
        }
        else
        {
          response.StatusCode = (int)HttpStatusCode.InternalServerError;
          errorResponse.Error = "database_error";
          errorResponse.ErrorDescription = "An error occurred while accessing the database.";
        }
        break;

      case PostgresException pgEx when pgEx.SqlState == "53300": // too many connections
      case NpgsqlException:
        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        errorResponse.Error = "database_unavailable";
        errorResponse.ErrorDescription = "Database is temporarily unavailable.";
        break;

      default:
        response.StatusCode = (int)HttpStatusCode.InternalServerError;
        errorResponse.Error = "internal_server_error";
        errorResponse.ErrorDescription = "An unexpected error occurred.";
        break;
    }

    var json = JsonSerializer.Serialize(errorResponse);
    await response.WriteAsync(json);
  }
}
