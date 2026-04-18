using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GraNAS.Shared.Correlation;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
  public async Task InvokeAsync(HttpContext context)
  {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrEmpty(correlationId))
    {
      correlationId = Guid.NewGuid().ToString();
    }

    context.TraceIdentifier = correlationId;

    using (logger.BeginScope(new Dictionary<string, object>
           {
             ["CorrelationId"] = correlationId
           }))
    {
      await next(context);
    }
  }
}
