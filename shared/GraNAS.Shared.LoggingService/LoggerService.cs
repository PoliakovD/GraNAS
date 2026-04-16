using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace GraNAS.Shared.LoggingService;

public interface ILoggerService
{
  Task LogInfo(string message, string? userId = null, object? additionalData = null);
  Task LogWarning(string message, string? userId = null, object? additionalData = null);
  Task LogError(string message, string? userId = null, object? additionalData = null);
}

public class LoggerService : ILoggerService
{
  private readonly ILogger<LoggerService> _logger;
  private readonly IHttpContextAccessor _httpContextAccessor;

  public LoggerService(ILogger<LoggerService> logger, IHttpContextAccessor httpContextAccessor)
  {
    _logger = logger;
    _httpContextAccessor = httpContextAccessor;
  }

  public Task LogInfo(string message, string? userId = null, object? additionalData = null)
  {
    WriteLog(LogLevel.Information, message, userId, additionalData);
    return Task.CompletedTask;
  }

  public Task LogWarning(string message, string? userId = null, object? additionalData = null)
  {
    WriteLog(LogLevel.Warning, message, userId, additionalData);
    return Task.CompletedTask;
  }

  public Task LogError(string message, string? userId = null, object? additionalData = null)
  {
    WriteLog(LogLevel.Error, message, userId, additionalData);
    return Task.CompletedTask;
  }

  private void WriteLog(LogLevel level, string message, string? userId, object? additionalData)
  {
    var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
    var additionalJson = additionalData != null ? JsonSerializer.Serialize(additionalData) : null;

    using var _ = LogContext.PushProperty("CorrelationId", correlationId);
    using var __ = LogContext.PushProperty("UserId", userId);
    using var ___ = LogContext.PushProperty("AdditionalData", additionalJson);

    _logger.Log(level, "{Message}", message);
  }
}
