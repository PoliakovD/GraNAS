using Serilog.Core;
using Serilog.Events;

namespace GraNAS.Shared.LoggingService.Enrichment;

public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> DefaultKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "email"
    };

    private readonly bool _isProduction;
    private readonly HashSet<string> _sensitiveKeys;
    private static readonly ScalarValue Masked = new("***MASKED***");

    public SensitiveDataEnricher(bool isProduction, IReadOnlyCollection<string>? sensitiveKeys = null)
    {
        _isProduction = isProduction;
        _sensitiveKeys = sensitiveKeys is not null
            ? new HashSet<string>(sensitiveKeys, StringComparer.OrdinalIgnoreCase)
            : DefaultKeys;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!_isProduction) return;

        foreach (var key in logEvent.Properties.Keys.ToList())
        {
            if (_sensitiveKeys.Contains(key))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(key, Masked));
            }
            else if (key == "Parameters" && logEvent.Properties[key] is StructureValue or DictionaryValue)
            {
                var masked = MaskValue(logEvent.Properties[key]);
                logEvent.AddOrUpdateProperty(new LogEventProperty(key, masked));
            }
        }
    }

    private LogEventPropertyValue MaskValue(LogEventPropertyValue value)
    {
        return value switch
        {
            StructureValue sv => new StructureValue(
                sv.Properties.Select(p =>
                    _sensitiveKeys.Contains(p.Name)
                        ? new LogEventProperty(p.Name, Masked)
                        : new LogEventProperty(p.Name, MaskValue(p.Value))),
                sv.TypeTag),
            DictionaryValue dv => new DictionaryValue(
                dv.Elements.Select(kv =>
                    new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        kv.Key,
                        _sensitiveKeys.Contains(kv.Key.Value?.ToString() ?? "")
                            ? Masked
                            : MaskValue(kv.Value)))),
            _ => value
        };
    }
}
