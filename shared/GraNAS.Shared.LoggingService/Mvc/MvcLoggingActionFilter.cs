using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog.Context;

namespace GraNAS.Shared.LoggingService.Mvc;

public sealed class MvcLoggingActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.DisplayName;
        var httpMethod = context.HttpContext.Request.Method;
        var parameters = SafeCopyArguments(context.ActionArguments);

        using var _1 = LogContext.PushProperty("ActionName", actionName);
        using var _2 = LogContext.PushProperty("Method", httpMethod);
        using var _3 = LogContext.PushProperty("Parameters", parameters, destructureObjects: true);

        await next();
    }

    private static Dictionary<string, object?> SafeCopyArguments(IDictionary<string, object?> args)
    {
        var result = new Dictionary<string, object?>(args.Count);
        foreach (var (key, value) in args)
        {
            if (value is Stream or IFormFile or IFormFileCollection or CancellationToken or HttpContext)
                continue;
            result[key] = value;
        }
        return result;
    }
}
